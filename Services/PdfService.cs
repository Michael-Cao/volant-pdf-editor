using System;
using System.Collections.Generic;
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
    public class PdfService
    {
        // 1. FAST INITIALIZATION (Line 19) - Reads basic structural size metrics on the main thread
        public List<PdfPageViewModel> InitializeSkeleton(string filePath)
        {
            var skeletonBuffer = new List<PdfPageViewModel>();
            try
            {
                using (PdfDocument document = PdfDocument.Open(filePath))
                {
                    for (int i = 1; i <= document.NumberOfPages; i++)
                    {
                        var page = document.GetPage(i);

                        skeletonBuffer.Add(new PdfPageViewModel
                        {
                            PageNumber = i,
                            Width = page.Width,
                            Height = page.Height,
                            FilePath = filePath,
                            IsRendered = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fast parse failed: {ex.Message}", "Parsing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return skeletonBuffer;
        }

        // 2. BACKGROUND THREAD RENDERER - Runs asynchronously on demand as the user scrolls
        public async Task RenderPageBackgroundAsync(PdfPageViewModel pageModel)
        {
            if (pageModel.IsRendered) return; // Prevent infinite re-rendering loops

            await Task.Run(() =>
            {
                try
                {
                    using (PdfDocument document = PdfDocument.Open(pageModel.FilePath))
                    {
                        document.AddSkiaPageFactory();
                        var page = document.GetPage(pageModel.PageNumber);

                        // Render background pixels asynchronously in the background thread pool
                        using (SKBitmap skiaBitmap = document.GetPageAsSKBitmap(pageModel.PageNumber, scale: 1.5f, SKColors.White))
                        using (SKImage skiaImage = SKImage.FromBitmap(skiaBitmap))
                        using (SKData encodedData = skiaImage.Encode(SKEncodedImageFormat.Png, 85)) // 85% compression saves massive RAM
                        {
                            byte[] bitmapBytes = encodedData.ToArray();

                            // Safely cross thread boundaries to update visual components back on the UI thread
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                BitmapImage wpfBitmap = new BitmapImage();
                                wpfBitmap.BeginInit();
                                wpfBitmap.StreamSource = new MemoryStream(bitmapBytes);
                                wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
                                wpfBitmap.EndInit();
                                wpfBitmap.Freeze(); // Freezing allows safe cross-thread image reading

                                pageModel.BackgroundImage = wpfBitmap;

                                // Build visual canvas component layout textboxes
                                pageModel.ComponentCanvas = new Canvas
                                {
                                    Background = Brushes.Transparent,
                                    Width = page.Width,
                                    Height = page.Height
                                };

                                BuildCanvasComponents(page, pageModel.ComponentCanvas);
                                pageModel.IsRendered = true;
                            });
                        }
                    }
                }
                catch { /* Quietly handle file disposal safety patterns */ }
            });
        }

        // Helper framework to parse character locations onto the WPF editing layer
        private void BuildCanvasComponents(UglyToad.PdfPig.Content.Page page, Canvas canvas)
        {
            canvas.Children.Clear();
            double pageHeight = page.Height;

            foreach (var word in page.GetWords())
            {
                // 📁 FIXED: Check if the word contains letters, then grab the FontSize of the first character [0]
                double extractedFontSize = word.Letters.Count > 0 ? word.Letters[0].FontSize : 12;

                TextBox textElement = new TextBox
                {
                    Text = word.Text,
                    FontSize = extractedFontSize, // Assign the specific numeric float size safely
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

                canvas.Children.Add(textElement);
            }
        }

        // UPGRADED: Pulls Page 1 immediately, then streams remaining skeletons in a non-blocking background task
        public List<PdfPageViewModel> LoadFirstPageImmediately(string filePath, out int totalPages)
        {
            var instantBuffer = new List<PdfPageViewModel>();
            totalPages = 0;

            try
            {
                using (PdfDocument document = PdfDocument.Open(filePath))
                {
                    totalPages = document.NumberOfPages;

                    if (totalPages > 0)
                    {
                        // Grab ONLY the first page metrics right away (takes a few milliseconds)
                        var firstPage = document.GetPage(1);
                        instantBuffer.Add(new PdfPageViewModel
                        {
                            PageNumber = 1,
                            Width = firstPage.Width,
                            Height = firstPage.Height,
                            FilePath = filePath,
                            IsRendered = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Instant load failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return instantBuffer;
        }

        // Background Worker: Computes sizes for the remaining pages without lagging the window
        public async Task<List<PdfPageViewModel>> BuildRemainingSkeletonsAsync(string filePath, int totalPages)
        {
            return await Task.Run(() =>
            {
                var remainingBuffer = new List<PdfPageViewModel>();
                try
                {
                    using (PdfDocument document = PdfDocument.Open(filePath))
                    {
                        // Start from Page 2 since Page 1 is already loaded
                        for (int i = 2; i <= totalPages; i++)
                        {
                            var page = document.GetPage(i);
                            remainingBuffer.Add(new PdfPageViewModel
                            {
                                PageNumber = i,
                                Width = page.Width,
                                Height = page.Height,
                                FilePath = filePath,
                                IsRendered = false
                            });
                        }
                    }
                }
                catch { /* Handle background file closing safety exceptions gracefully */ }
                return remainingBuffer;
            });
        }
    }
}
