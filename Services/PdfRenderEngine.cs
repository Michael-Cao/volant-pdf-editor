using pdf_editor2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

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

        private readonly PdfPageResolver _pageResolver;

        private readonly PdfPageCache _pageCache;

        public PdfRenderEngine(PdfPageResolver pageResolver, PdfPageCache pageCache)
        {
            _pageResolver = pageResolver;
            _pageCache = pageCache;
        }

        public void StartStreamingDocument(string filePath)
        {
            Task.Run(() =>
            {
                try
                {
                    PdfStructureInfo structureInfo = PdfStructureParser.ParseLowLevelStructure(filePath);

                    using (PdfDocument document = PdfDocument.Open(filePath))
                    {
                        int totalPages = document.NumberOfPages;
                        structureInfo.TotalObjectsCount = document.Structure.Catalog.CatalogDictionary?.Data.Count ?? totalPages;

                        for (int i = 1; i <= totalPages; i++)
                        {
                            var page = document.GetPage(i);
                            double pageHeight = page.Height;

                            var wordModels = page.GetWords().Select(word => new PdfWordData
                            {
                                Text = word.Text,
                                FontSize = word.Letters.Count > 0 ? word.Letters[0].FontSize : 12,
                                X = word.BoundingBox.Left,
                                Y = pageHeight - word.BoundingBox.Top
                            }).ToList();

                            // --- OPTIMIZED CACHED PIPELINE LAYER ---
                            // Check cache map first to eliminate parsing overhead on reload
                            PdfPageStructure pageStructure = _pageCache.Get(filePath, i);

                            if (pageStructure == null && page.Dictionary is DictionaryToken dictToken)
                            {
                                var dictionaryWrapper = dictToken.Data.ToDictionary(
                                    kvp => kvp.Key,
                                    kvp => (object)kvp.Value
                                );

                                // Only execute structural resolution if cache missed
                                pageStructure = _pageResolver.PreparePageObjects(dictionaryWrapper);

                                // Hydrate the cache for subsequent UI view steps
                                _pageCache.Put(filePath, i, pageStructure);
                            }

                            var pageModel = new PdfPageViewModel
                            {
                                PageNumber = i,
                                Width = page.Width,
                                Height = page.Height,
                                FilePath = filePath,
                                WordsData = wordModels,
                                IsRendered = false,
                                Structure = structureInfo,
                                PageStructure = pageStructure
                            };

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
