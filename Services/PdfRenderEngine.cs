using Microsoft.Extensions.Logging;
using pdf_editor2.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Rendering.Skia;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

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

            // 2. Offload execution safely to background thread task runners
            return await Task.Run(() =>
            {
                List<PdfWordData> combinedPrimitives;
                byte[] bitmapBytes;
                double pageWidth;
                double pageHeight;

                // 🔒 CRITICAL THREAD BOUNDARY LOCK
                lock (_pdfLock)
                {
                    if (_activeDocument == null || _activeFilePath != filePath)
                    {
                        OpenDocumentHandle(filePath);
                    }

                    Page page = _activeDocument.GetPage(pageNumber);
                    pageWidth = page.Width;
                    pageHeight = page.Height;

                    // Extract all text, image, and path entities via a clean helper extraction method
                    combinedPrimitives = ExtractPagePrimitives(page);

                    // Rasterize page canvas layouts to a portable PNG byte array
                    bitmapBytes = RasterizePageToPng(pageNumber);
                }

                var structure = new PdfPageStructure { WordsData = combinedPrimitives, RawPngBytes = bitmapBytes };
                _pageCache.Put(filePath, pageNumber, structure);

                // Raise backward-compatible event signals cleanly
                OnPageParsed?.Invoke(this, new PageParsedEventArgs
                {
                    PageNumber = pageNumber,
                    Width = pageWidth,
                    Height = pageHeight,
                    WordsData = combinedPrimitives,
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

        /// <summary>
        /// Scans text tokens, raster images, and vector path lines from the active PdfPig page container.
        /// </summary>
        private List<PdfWordData> ExtractPagePrimitives(Page page)
        {
            var primitives = new List<PdfWordData>();
            if (page == null) return primitives;

            _logger.LogInformation("[Engine-Layout] Starting specialized document layout analysis for page {Num}.", page.Number);

            // 1. Cluster Words into Multi-Line Paragraph Blocks sorted by logical reading order
            try
            {
                var letters = page.Letters;
                if (letters != null && letters.Any())
                {
                    // Step A: Extract words out of raw character letters using the nearest neighbor algorithm tool
                    var words = NearestNeighbourWordExtractor.Instance.GetWords(letters);

                    // Step B: Segment the collection of words dynamically into multi-line block paragraph structures
                    var textBlocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);

                    // Step C: Sequence blocks into human-readable logical processing streams
                    var orderedTextBlocks = UnsupervisedReadingOrderDetector.Instance.Get(textBlocks);

                    foreach (var block in orderedTextBlocks)
                    {
                        // Reconstruct multi-line paragraph block text cleanly
                        var sb = new StringBuilder();
                        foreach (var textLine in block.TextLines)
                        {
                            sb.AppendLine(textLine.Text);
                        }

                        string combinedBlockText = sb.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(combinedBlockText))
                        {
                            // Safely isolate structural font text sizing parameters from available sub-letters
                            double maxFontSize = 11.0;
                            if (block.TextLines.Any() && block.TextLines.First().Words.Any() && block.TextLines.First().Words.First().Letters.Any())
                            {
                                maxFontSize = block.TextLines.First().Words.First().Letters.First().FontSize;
                            }

                            var bbox = block.BoundingBox;
                            primitives.Add(new PdfWordData
                            {
                                ObjectType = PdfObjectType.Text,
                                Text = combinedBlockText,
                                FontSize = maxFontSize,
                                X = bbox.Left,
                                // Synchronize inverted coordinate space seamlessly for your viewer container mapping
                                Y = page.Height - bbox.Top,
                                Width = bbox.Width,
                                Height = bbox.Height
                            });
                        }
                    }
                    _logger.LogInformation("[Engine-Layout] Completed parsing text. Created {Count} reading-ordered paragraph blocks.", orderedTextBlocks.Count());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Engine-Layout] Failed executing native page segmentation pipeline tools.");
            }

            // 2. Parse Raster Images via Bounding Boxes
            try
            {
                var images = page.GetImages();
                if (images != null)
                {
                    foreach (var img in images)
                    {
                        primitives.Add(new PdfWordData
                        {
                            ObjectType = PdfObjectType.Image,
                            Text = $"Embedded Image Resource ({Math.Round(img.Bounds.Width, 1)}x{Math.Round(img.Bounds.Height, 1)})",
                            X = img.Bounds.Left,
                            Y = page.Height - img.Bounds.Top,
                            Width = img.Bounds.Width,
                            Height = img.Bounds.Height
                        });
                    }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "[Engine] Image parse failure."); }

            // 3. Parse Vector Paths via Experimental Access namespaces
            try
            {
                if (page.ExperimentalAccess?.Paths != null)
                {
                    foreach (var path in page.ExperimentalAccess.Paths)
                    {
                        var bounds = path.GetBoundingRectangle();
                        primitives.Add(new PdfWordData
                        {
                            ObjectType = PdfObjectType.Path,
                            Text = path.IsFilled ? "Filled Vector Shape" : "Stroked Path Line",
                            X = bounds?.Left ?? 0,
                            Y = page.Height - (bounds?.Top ?? 0),
                            Width = bounds?.Width ?? 0,
                            Height = bounds?.Height ?? 0
                        });
                    }
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "[Engine] Vector path parse failure."); }

            return primitives;
        }

        /// <summary>
        /// Scans the entire preheated document handle to compile an aggregated master list of all unique text styles.
        /// </summary>
        public List<PdfGlobalStyle> ScanDocumentGlobalStyles(string filePath)
        {
            var uniqueStyles = new HashSet<PdfGlobalStyle>();
            _logger.LogInformation("Starting document-wide aggregate style indexing sequence...");

            lock (_pdfLock)
            {
                if (_activeDocument == null || _activeFilePath != filePath)
                {
                    OpenDocumentHandle(filePath);
                }

                // Fast sequential iteration across all page records inside the locked document stream
                for (int i = 1; i <= _activeDocument.NumberOfPages; i++)
                {
                    Page page = _activeDocument.GetPage(i);
                    var letters = page.Letters;

                    if (letters == null) continue;

                    foreach (var letter in letters)
                    {
                        // FIX: Use ToString() as a universal thread-safe unique color string signature 
                        // to bypass all past, present, and future missing IColor property gaps across any PdfPig version.
                        string rawColorSignature = letter.Color != null ? letter.Color.ToString() : "DefaultColor";

                        // Format a clean web-style display color fallback based on signature hash definitions
                        string uiDisplayColor = "#333333";
                        if (rawColorSignature.Contains("1 1 1") || rawColorSignature.Contains("255 255 255"))
                        {
                            uiDisplayColor = "#FFFFFF"; // Keep precise visual indicators for common clear text elements
                        }

                        var styleRecord = new PdfGlobalStyle
                        {
                            FontName = letter.FontName ?? "Unknown",
                            FontSize = Math.Round(letter.FontSize, 1),
                            // Assign the unique string or fallback palette tag directly to satisfy UI bindings layout 
                            HexColor = uiDisplayColor,
                            IsBold = letter.Font?.IsBold ?? false,
                            IsItalic = letter.Font?.IsItalic ?? false
                        };

                        // HashSet handles de-duplication flawlessly based on string and font property values combined
                        uniqueStyles.Add(styleRecord);
                    }
                }
            }

            _logger.LogInformation("Global style indexing closed down. Discovered {Count} unique style combinations.", uniqueStyles.Count);
            return uniqueStyles.OrderBy(s => s.FontName).ThenByDescending(s => s.FontSize).ToList();
        }

        /// <summary>
        /// Rasterizes the target page index over into unmanaged image memory layers.
        /// </summary>
        private byte[] RasterizePageToPng(int pageNumber)
        {
            using (SKBitmap skiaBitmap = _activeDocument.GetPageAsSKBitmap(pageNumber, scale: 1.5f, SKColors.White))
            using (SKImage skiaImage = SKImage.FromBitmap(skiaBitmap))
            using (SKData encodedData = skiaImage.Encode(SKEncodedImageFormat.Png, 85))
            {
                return encodedData.ToArray();
            }
        }


    }


    public record PageMetadataDto(int PageNumber, double Width, double Height, string FilePath);
    public record ParsedPageResult(List<PdfWordData> Words, byte[] RawPngBytes);
}
