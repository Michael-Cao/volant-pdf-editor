using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using pdf_editor2.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;
using SkiaSharp;

namespace pdf_editor2.Services
{
    // Custom event argument package to wrap around our page data payloads
    public class PageParsedEventArgs : EventArgs
    {
        public PdfPageViewModel PageViewModel { get; set; }
        public int TotalPages { get; set; }
    }

    public class PdfRenderEngine
    {
        // ⚡ The Event Handler Hook that the Main Application will listen to
        public event EventHandler<PageParsedEventArgs> OnPageParsed;

        /// <summary>
        /// Asynchronously streams through a PDF document, rendering and parsing page by page, 
        /// emitting an event instantly as soon as each individual page completes processing.
        /// </summary>
        public void StartStreamingDocument(string filePath)
        {
            // Spin off a dedicated background core thread pool worker
            Task.Run(() =>
            {
                try
                {
                    using (PdfDocument document = PdfDocument.Open(filePath))
                    {
                        // Register Skia engine rules natively to this specific background instance handle
                        document.AddSkiaPageFactory();
                        int totalPages = document.NumberOfPages;

                        for (int i = 1; i <= totalPages; i++)
                        {
                            var page = document.GetPage(i);
                            double pageHeight = page.Height;

                            // 1. PROCESS AND BUFFER VISUAL GRAPHICS BITMAPS
                            BitmapImage wpfBitmap;
                            using (SKBitmap skiaBitmap = document.GetPageAsSKBitmap(i, scale: 1.5f, SKColors.White))
                            using (SKImage skiaImage = SKImage.FromBitmap(skiaBitmap))
                            using (SKData encodedData = skiaImage.Encode(SKEncodedImageFormat.Png, 85))
                            using (Stream imageStream = encodedData.AsStream())
                            {
                                wpfBitmap = new BitmapImage();
                                wpfBitmap.BeginInit();
                                wpfBitmap.StreamSource = imageStream;
                                wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
                                wpfBitmap.EndInit();
                                wpfBitmap.Freeze(); // Freeze allows cross-thread UI consumption safety rules
                            }

                            // 2. CONSTRUCT LIVE INTERACTIVE ELEMENT LAYER ON THE STA THREAD BOUNDARY
                            Canvas pageCanvas = null;
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                pageCanvas = new Canvas
                                {
                                    Background = Brushes.Transparent,
                                    Width = page.Width,
                                    Height = page.Height
                                };

                                foreach (var word in page.GetWords())
                                {
                                    // 📁 FIXED: Check if the word contains letters, then grab the FontSize of the first character safely
                                    double extractedFontSize = word.Letters.Count > 0 ? word.Letters[0].FontSize : 12;

                                    TextBox textElement = new TextBox
                                    {
                                        Text = word.Text,
                                        FontSize = extractedFontSize, // Assign the font size safely
                                        FontFamily = new FontFamily("Arial"),
                                        Background = Brushes.Transparent,
                                        BorderThickness = new Thickness(0),
                                        Padding = new Thickness(0),
                                        Cursor = System.Windows.Input.Cursors.IBeam
                                    };

                                    textElement.GotFocus += (s, e) => { textElement.BorderThickness = new Thickness(1); textElement.BorderBrush = Brushes.DodgerBlue; };
                                    textElement.LostFocus += (s, e) => { textElement.BorderThickness = new Thickness(0); };

                                    double xPos = word.BoundingBox.Left;
                                    double yPos = pageHeight - word.BoundingBox.Top;

                                    Canvas.SetLeft(textElement, xPos);
                                    Canvas.SetTop(textElement, yPos);

                                    pageCanvas.Children.Add(textElement);
                                }
                            });

                            // 3. PACKAGE PAYLOAD ENVELOPE
                            var pageModel = new PdfPageViewModel
                            {
                                PageNumber = i,
                                Width = page.Width,
                                Height = page.Height,
                                BackgroundImage = wpfBitmap,
                                ComponentCanvas = pageCanvas,
                                IsRendered = true,
                                FilePath = filePath
                            };

                            // 4. FIRE EVENT STREAM BROADCAST OUTWARD
                            // Invokes the listener method immediately
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
    }
}
