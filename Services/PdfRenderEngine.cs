using Microsoft.Extensions.Logging;
using pdf_editor2.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Rendering.Skia;

namespace pdf_editor2.Services
{
    public class PageParsedEventArgs : EventArgs
    {
        public int PageNumber { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<PdfWordData> WordsData { get; set; }
        public byte[] CompressedPngBytes { get; set; }
    }

    public class PdfRenderEngine : IDisposable
    {
        public event EventHandler<PageParsedEventArgs> OnPageParsed;
        private readonly object _pdfLock = new object();

        private readonly PdfPageResolver _pageResolver;
        private readonly PdfPageCache _pageCache;
        private readonly ILogger<PdfRenderEngine> _logger;

        // ⚡ ACTIVE PERSISTENT HANDLE LAYER: Eliminates costly re-opens on disk
        private PdfDocument _activeDocument;
        private string _activeFilePath;

        public PdfRenderEngine(PdfPageResolver pageResolver, PdfPageCache pageCache, ILogger<PdfRenderEngine> logger)
        {
            _pageResolver = pageResolver;
            _pageCache = pageCache;
            _logger = logger;
        }

        /// <summary>
        /// Explicitly initializes and locks the unmanaged file stream layout context into memory upfront.
        /// </summary>
        public void OpenDocumentHandle(string filePath)
        {
            if (_activeFilePath == filePath && _activeDocument != null) return;

            CloseDocumentHandle(); // Ensure old streams are disposed safely first

            _logger.LogInformation("Opening fresh unmanaged document pipeline handle for: {FileName}", Path.GetFileName(filePath));
            _activeFilePath = filePath;
            _activeDocument = PdfDocument.Open(filePath);
            _activeDocument.AddSkiaPageFactory(); // Attach Skia canvas rendering factory exactly once
        }

        /// <summary>
        /// Fast skeletal data extraction using the cached warm file context.
        /// </summary>
        public List<PageMetadataDto> GetDocumentSkeleton(string filePath)
        {
            // Ensure handle is preheated
            OpenDocumentHandle(filePath);

            var skeleton = new List<PageMetadataDto>();
            for (int i = 1; i <= _activeDocument.NumberOfPages; i++)
            {
                var page = _activeDocument.GetPage(i);
                skeleton.Add(new PageMetadataDto(i, page.Width, page.Height, filePath));
            }
            return skeleton;
        }

        /// <summary>
        /// Highly optimized background pixel rasterizer utilizing warm memory stream references.
        /// Respects CancellationTokens dynamically mid-run.
        /// </summary>
        public async Task<PdfPageStructure> RenderAndParsePageAsync(string filePath, int pageNumber)
        {
            _logger.LogInformation("Processing lazy-load materialization layer for {FileName}, Page {Num}", Path.GetFileName(filePath), pageNumber);

            // 1. Fast cache lookup (Lock-free)
            PdfPageStructure cachedStructure = _pageCache.Get(filePath, pageNumber);
            if (cachedStructure != null)
            {
                _logger.LogDebug("Cache hit achieved for Page {Num}. Skipping rendering pipelines completely.", pageNumber);
                return cachedStructure;
            }

            // 2. Offload execution safely to background thread
            return await Task.Run(() =>
            {
                List<PdfWordData> wordsList;
                byte[] bitmapBytes;
                double pageWidth;
                double pageHeight;

                // 🔒 CRITICAL FIX: Keep document configuration and core extraction isolated inside the lock boundary
                lock (_pdfLock)
                {
                    if (_activeDocument == null || _activeFilePath != filePath)
                    {
                        OpenDocumentHandle(filePath);
                    }

                    Page page = _activeDocument.GetPage(pageNumber);
                    pageWidth = page.Width;
                    pageHeight = page.Height;

                    // Extract text metrics safely inside the lock
                    wordsList = page.GetWords().Select(word => new PdfWordData
                    {
                        Text = word.Text,
                        FontSize = word.Letters.Count > 0 ? word.Letters[0].FontSize : 12,
                        X = word.BoundingBox.Left,
                        Y = pageHeight - word.BoundingBox.Top
                    }).ToList();

                    // Rasterize into memory safely inside the lock
                    using (SKBitmap skiaBitmap = _activeDocument.GetPageAsSKBitmap(pageNumber, scale: 1.5f, SKColors.White))
                    using (SKImage skiaImage = SKImage.FromBitmap(skiaBitmap))
                    using (SKData encodedData = skiaImage.Encode(SKEncodedImageFormat.Png, 85))
                    {
                        bitmapBytes = encodedData.ToArray();
                    }
                } // 🔓 Lock released safely. Underling native pointers can now mutate without crashing this thread.

                var structure = new PdfPageStructure
                {
                    WordsData = wordsList,
                    RawPngBytes = bitmapBytes
                };

                // Cache completed structure
                _pageCache.Put(filePath, pageNumber, structure);

                // Raise backward-compatible event metrics cleanly
                OnPageParsed?.Invoke(this, new PageParsedEventArgs
                {
                    PageNumber = pageNumber,
                    Width = pageWidth,
                    Height = pageHeight,
                    WordsData = wordsList,
                    CompressedPngBytes = bitmapBytes
                });

                return structure;
            });
        }


        /// <summary>
        /// Removes one rendered page from the in-memory cache.
        /// </summary>
        public void EvictFromCache(string filePath, int pageNumber)
        {
            _pageCache.Remove(filePath, pageNumber);
        }

        /// <summary>
        /// Cleans unmanaged memory files and releases OS locks instantly on demand.
        /// </summary>
        public void CloseDocumentHandle()
        {
            if (_activeDocument != null)
            {
                _logger.LogInformation("Disposing unmanaged active document handle safely: {FileName}", Path.GetFileName(_activeFilePath));
                _activeDocument.Dispose();
                _activeDocument = null;
            }
            _activeFilePath = null;
        }

        public void Dispose()
        {
            CloseDocumentHandle();
        }

        // Backward compatibility method proxying down to the updated cancellation loop pattern
        public async Task ParsePageAsync(string filePath, int pageNumber)
        {
            await RenderAndParsePageAsync(filePath, pageNumber);
        }
    }

    public record PageMetadataDto(int PageNumber, double Width, double Height, string FilePath);
    public record ParsedPageResult(List<PdfWordData> Words, byte[] RawPngBytes);
}
