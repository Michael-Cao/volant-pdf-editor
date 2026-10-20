using pdf_editor2.Models;
using SkiaSharp;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Rendering.Skia;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Fonts.Standard14Fonts;

namespace pdf_editor2.Services
{
    public class PdfService
    {
        // 1. FAST INITIALIZATION - Reads basic structural size metrics on the main thread (Seconds for 1000+ pages)
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

        // 2. BACKGROUND THREAD RENDERER & LAYOUT PARSER - Runs asynchronously on demand as the user scrolls
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
                        double pageHeight = page.Height;

                        // Step A: Extract lightweight text layout POCO models on the background thread
                        var wordsList = page.GetWords().Select(word => new PdfWordData
                        {
                            Text = word.Text,
                            FontSize = word.Letters.Count > 0 ? word.Letters[0].FontSize : 12,
                            X = word.BoundingBox.Left,
                            Y = pageHeight - word.BoundingBox.Top // Convert PDF coordinate rules to WPF systems
                        }).ToList();

                        // Step B: Render background pixels into compressed memory streams safely
                        using (SKBitmap skiaBitmap = document.GetPageAsSKBitmap(pageModel.PageNumber, scale: 1.5f, SKColors.White))
                        using (SKImage skiaImage = SKImage.FromBitmap(skiaBitmap))
                        using (SKData encodedData = skiaImage.Encode(SKEncodedImageFormat.Png, 85)) // 85% compression saves massive RAM
                        {
                            byte[] bitmapBytes = encodedData.ToArray();

                            // Step C: Marshal back to UI context ONLY to populate decoupled properties
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                BitmapImage wpfBitmap = new BitmapImage();
                                wpfBitmap.BeginInit();
                                wpfBitmap.StreamSource = new MemoryStream(bitmapBytes);
                                wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
                                wpfBitmap.EndInit();
                                wpfBitmap.Freeze(); // Freeze allows safe thread-neutral consumption 

                                // Update properties; triggers UI binding updates cleanly
                                pageModel.WordsData = wordsList;
                                pageModel.BackgroundImage = wpfBitmap;
                                pageModel.IsRendered = true;
                            });
                        }
                    }
                }
                catch { /* Quietly handle file disposal safety patterns */ }
            });
        }

        // 3. RAPID RECOVERY ENGINE - Instantly clears image buffers and structural UI nodes when scrolling out of view
        public void UnloadPageFromMemory(PdfPageViewModel pageModel)
        {
            if (!pageModel.IsRendered) return;

            // Instantly clear memory references, freeing heap generation states to garbage collectors
            pageModel.BackgroundImage = null;
            pageModel.WordsData = null; // Clears the list so ItemsControl unloads textboxes entirely
            pageModel.IsRendered = false;
        }

        // 4. INSTANT HEAD-LESS EXTRACTOR - Pulls Page 1 metadata parameters immediately
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

        // 5. ASYNC CORE BACKGROUND COMPUTER - Populates placeholders for pages 2 to N
        public async Task<List<PdfPageViewModel>> BuildRemainingSkeletonsAsync(string filePath, int totalPages)
        {
            return await Task.Run(() =>
            {
                var remainingBuffer = new List<PdfPageViewModel>();
                try
                {
                    using (PdfDocument document = PdfDocument.Open(filePath))
                    {
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
                catch { /* Handle unexpected parsing exceptions safely */ }
                return remainingBuffer;
            });
        }

        // <summary>
        /// Compiles the text data modifications back into a crisp, vector-based native PDF file.
        /// This bypasses image flattening, preserving small file sizes and keeping text selectable.
        /// </summary>
        public async Task SaveDocumentAsync(string outputFilePath, IList<PdfPageViewModel> pageModels)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Initialize the vector document creation pipeline
                    using (var builder = new PdfDocumentBuilder())
                    {
                        foreach (var pageModel in pageModels)
                        {
                            // Create a new blank vector canvas targeting the original structural proportions
                            var builderPage = builder.AddPage(pageModel.Width, pageModel.Height);

                            PdfDocumentBuilder.AddedFont helveticaFont = builder.AddStandard14Font(Standard14Font.Helvetica);

                            // Safeguard: Ensure layout coordinates are populated before drawing elements
                            if (pageModel.WordsData != null)
                            {
                                foreach (var word in pageModel.WordsData)
                                {
                                    // Reverse the coordinate mapping logic back into native PDF Cartesian grids
                                    // Y = PageHeight - WPF_Y_Position
                                    double pdfYPosition = pageModel.Height - word.Y;

                                    // Render native text string commands directly onto the binary streams
                                    builderPage.AddText(
                                        word.Text,
                                        word.FontSize,
                                        new PdfPoint(word.X, pdfYPosition),
                                        helveticaFont
                                    );
                                }
                            }
                        }

                        // Flush the binary vector compilation out to disk
                        byte[] compiledPdfBytes = builder.Build();
                        File.WriteAllBytes(outputFilePath, compiledPdfBytes);
                    }
                }
                catch (IOException ex)
                {
                    throw new IOException($"Failed to compile native vector document layout: {ex.Message}", ex);
                }
            });
        }
    }
}
