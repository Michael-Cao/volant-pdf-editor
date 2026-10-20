using System;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using pdf_editor2.Models;
using UglyToad.PdfPig;

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
        /// </summary>
        public void StartStreamingDocument(string filePath)
        {
            Task.Run(() =>
            {
                try
                {
                    // 1. Call decoupled static structural worker to read file primitives
                    PdfStructureInfo structureInfo = PdfStructureParser.ParseLowLevelStructure(filePath);

                    // Open the document temporarily to extract page layout structural data
                    using (PdfDocument document = PdfDocument.Open(filePath))
                    {
                        int totalPages = document.NumberOfPages;

                        if (document.Structure.Catalog.CatalogDictionary?.Data != null)
                        {
                            structureInfo.TotalObjectsCount = document.Structure.Catalog.CatalogDictionary.Data.Count;
                        }
                        else
                        {
                            structureInfo.TotalObjectsCount = document.NumberOfPages; // Fallback metric if token structure is null
                        }

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
                                Y = pageHeight - word.BoundingBox.Top
                            }).ToList();

                            var pageModel = new PdfPageViewModel
                            {
                                PageNumber = i,
                                Width = page.Width,
                                Height = page.Height,
                                FilePath = filePath,
                                WordsData = wordModels,
                                IsRendered = false,
                                Structure = structureInfo // Attach shared low-level structural context
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
    }

    public class PdfWordData
    {
        public string Text { get; set; }
        public double FontSize { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
