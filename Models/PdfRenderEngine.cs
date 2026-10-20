using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using pdf_editor2.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;
using SkiaSharp;
using System.Linq;

namespace pdf_editor2.Services
{
    public class PageParsedEventArgs : EventArgs
    {
        public PdfPageViewModel PageViewModel { get; set; }
        public int TotalPages { get; set; }
    }

    public class PdfRenderEngine
    {
        public event EventHandler<PageParsedEventArgs> OnPageParsed;

        /// <summary>
        /// Asynchronously reads document metadata and text layouts on a background thread.
        /// Does NOT render images or allocate heavy UI elements upfront.
        /// </summary>
        public void StartStreamingDocument(string filePath)
        {
            Task.Run(() =>
            {
                try
                {
                    // Open the document temporarily to extract structural data, then release file locks
                    using (PdfDocument document = PdfDocument.Open(filePath))
                    {
                        int totalPages = document.NumberOfPages;

                        for (int i = 1; i <= totalPages; i++)
                        {
                            var page = document.GetPage(i);
                            double pageHeight = page.Height;

                            // Extract text metadata into a lightweight POCO collection
                            var wordModels = page.GetWords().Select(word => new PdfWordData
                            {
                                Text = word.Text,
                                FontSize = word.Letters.Count > 0 ? word.Letters[0].FontSize : 12,
                                X = word.BoundingBox.Left,
                                Y = pageHeight - word.BoundingBox.Top // Convert PDF coordinate system to WPF rules
                            }).ToList();

                            // Instantiate a lightweight shell ViewModel
                            var pageModel = new PdfPageViewModel
                            {
                                PageNumber = i,
                                Width = page.Width,
                                Height = page.Height,
                                FilePath = filePath,
                                WordsData = wordModels, // Store layout data only; no UI Canvas references
                                IsRendered = false      // Defaults to unrendered until scrolled into viewport
                            };

                            // Stream payload event outward to the UI thread sequentially
                            OnPageParsed?.Invoke(this, new PageParsedEventArgs
                            {
                                PageViewModel = pageModel,
                                TotalPages = totalPages
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show($"Streaming engine failure: {ex.Message}", "Engine Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        /// <summary>
        /// On-Demand Dynamic Renderer. 
        /// Triggered exclusively when a specific page enters the UI viewport container.
        /// </summary>
        public static async Task<BitmapImage> RenderPageImageAsync(string filePath, int pageNumber, float scale = 1.5f)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (PdfDocument document = PdfDocument.Open(filePath))
                    {
                        document.AddSkiaPageFactory();

                        using (SKBitmap skiaBitmap = document.GetPageAsSKBitmap(pageNumber, scale, SKColors.White))
                        using (SKImage skiaImage = SKImage.FromBitmap(skiaBitmap))
                        using (SKData encodedData = skiaImage.Encode(SKEncodedImageFormat.Png, 85))
                        using (Stream imageStream = encodedData.AsStream())
                        {
                            BitmapImage wpfBitmap = new BitmapImage();
                            wpfBitmap.BeginInit();
                            wpfBitmap.StreamSource = imageStream;
                            wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
                            wpfBitmap.EndInit();
                            wpfBitmap.Freeze(); // Freeze grants cross-thread UI consumption safety rules
                            return wpfBitmap;
                        }
                    }
                }
                catch
                {
                    return null; // Fallback placeholder asset can be returned here if execution fails
                }
            });
        }
    }

    /// <summary>
    /// Lightweight spatial layout model used to decouple text parsing data from UI control trees.
    /// </summary>
    public class PdfWordData
    {
        public string Text { get; set; }
        public double FontSize { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
