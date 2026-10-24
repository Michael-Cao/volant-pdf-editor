using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using pdf_editor2.Models;

namespace pdf_editor2.Services
{
    /// <summary>
    /// High-performance document layout analysis service responsible for parsing 
    /// PDF content streams into lightweight visual primitives.
    /// </summary>
    public class PdfLayoutEngine
    {
        private readonly ILogger<PdfLayoutEngine> _logger;
        private readonly PdfFontMetricsManager _metricsManager;
        private readonly Dictionary<string, PdfFontMetrics> _globalFontCache = new Dictionary<string, PdfFontMetrics>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfLayoutEngine"/> class.
        /// </summary>
        /// <param name="logger">The diagnostic log pipeline provider.</param>
        public PdfLayoutEngine(PdfFontMetricsManager metricsManager, ILogger<PdfLayoutEngine> logger)
        {
            _metricsManager = metricsManager ?? throw new ArgumentNullException(nameof(metricsManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Clears the global font metrics cache. Call this whenever a new document is opened.
        /// </summary>
        public void ClearCache()
        {
            lock (_globalFontCache)
            {
                _globalFontCache.Clear();
                _logger.LogDebug("[Engine-Fonts] Global flyweight font metrics cache cleared successfully.");
            }
        }

        /// <summary>
        /// Parses a complete PDF page layout stream into unified structural data components.
        /// </summary>
        /// <param name="page">The source raw page state extracted via UglyToad.PdfPig.</param>
        /// <returns>A flat sequential list of positioned visual primitive structures.</returns>
        public List<PdfWordData> ExtractPagePrimitives(Page page)
        {
            var primitives = new List<PdfWordData>();
            if (page == null) return primitives;

            _logger.LogInformation("[Engine-Layout] Starting layout analysis for page {Num}.", page.Number);

            var uniqueFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Process typography and individual text structures
            ParseTextPrimitives(page, primitives, uniqueFonts);

            if (uniqueFonts.Any())
            {
                string fontListString = string.Join(", ", uniqueFonts.OrderBy(f => f));
                _logger.LogInformation("[Engine-Fonts] Fonts used on Page {Num}: [{FontList}]", page.Number, fontListString);
            }
            else
            {
                _logger.LogInformation("[Engine-Fonts] No text fonts detected on Page {Num}.", page.Number);
            }


            // 2. Process compressed asset raster binaries
            ParseImagePrimitives(page, primitives);

            // 3. Process geometric line vectors and structural curves
            ParsePathPrimitives(page, primitives);

            return primitives;
        }

        private void ParseTextPrimitives(Page page, List<PdfWordData> primitives, HashSet<string> uniqueFonts)
        {
            try
            {
                var letters = page.Letters;
                if (letters == null || !letters.Any()) return;

                var words = NearestNeighbourWordExtractor.Instance.GetWords(letters);
                foreach (var word in words)
                {
                    string cleanWordText = word.Text?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(cleanWordText)) continue;

                    if (IsWatermarkOrOutOfBounds(word, page, cleanWordText)) continue;

                    ExtractLetterMetadata(word, out double wordFontSize, out string foregroundColor, out string rawPostScriptName, out var firstLetter);

                    uniqueFonts.Add(rawPostScriptName);

                    // 🚀 DELEGATED: Style resolution now belongs exclusively to our external manager file
                    PdfFontMetrics sharedMetrics = _metricsManager.GetOrCreateFontMetrics(rawPostScriptName, firstLetter);

                    primitives.Add(new PdfWordData
                    {
                        ObjectType = PdfObjectType.Text,
                        Text = cleanWordText,
                        FontSize = wordFontSize,
                        X = word.BoundingBox.Left,
                        Y = word.BoundingBox.Bottom,
                        Width = word.BoundingBox.Width,
                        Height = word.BoundingBox.Height,
                        ForegroundColor = foregroundColor,
                        StyleMetrics = sharedMetrics
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Engine-Layout] Failed executing word extraction parsing routine.");
            }
        }

        private bool IsWatermarkOrOutOfBounds(Word word, Page page, string cleanWordText)
        {
            if (cleanWordText.IndexOf("Licensed to", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            var bbox = word.BoundingBox;
            double edgePadding = 2.0;

            return bbox.Left >= (page.Width + edgePadding) ||
                   bbox.Bottom >= (page.Height + edgePadding) ||
                   bbox.Right <= -edgePadding;
        }

        private void ExtractLetterMetadata(Word word, out double wordFontSize, out string foregroundColor, out string rawPostScriptName, out Letter? firstLetter)
        {
            wordFontSize = 11.0;
            foregroundColor = "#000000";
            rawPostScriptName = "Segoe UI";

            firstLetter = word.Letters.FirstOrDefault();
            if (firstLetter == null) return;

            wordFontSize = firstLetter.FontSize;
            rawPostScriptName = firstLetter.FontName ?? "Segoe UI";

            if (firstLetter.Color != null)
            {
                var rgb = firstLetter.Color.ToRGBValues();
                foregroundColor = $"#{(int)(rgb.r * 255):X2}{(int)(rgb.g * 255):X2}{(int)(rgb.b * 255):X2}";
            }
        }

        private void ParseImagePrimitives(Page page, List<PdfWordData> primitives)
        {
            try
            {
                var images = page.GetImages();
                if (images == null) return;

                foreach (var img in images)
                {
                    byte[]? rawBytes = null;

                    // Only perform a clean, decoupled fast memory array extraction inside the background worker
                    if (img.RawBytes != null && img.RawBytes.Length > 0)
                    {
                        rawBytes = img.RawBytes.ToArray();
                    }

                    primitives.Add(new PdfWordData
                    {
                        ObjectType = PdfObjectType.Image,
                        Text = $"Embedded Image Resource ({Math.Round(img.Bounds.Width, 1)}x{Math.Round(img.Bounds.Height, 1)})",
                        X = img.Bounds.Left,
                        Y = img.Bounds.Bottom,
                        Width = img.Bounds.Width,
                        Height = img.Bounds.Height,
                        ImageDataPayload = rawBytes // Store pure data only (highly thread-safe)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Engine] Image binary stream process failure.");
            }
        }


        private void ParsePathPrimitives(Page page, List<PdfWordData> primitives)
        {
            try
            {
                if (page.ExperimentalAccess?.Paths == null) return;

                foreach (var path in page.ExperimentalAccess.Paths)
                {
                    var nullableBounds = path.GetBoundingRectangle();
                    if (nullableBounds == null) continue;

                    // Extract the underlying non-nullable PdfRectangle struct safely
                    var bounds = nullableBounds.Value;

                    primitives.Add(new PdfWordData
                    {
                        ObjectType = PdfObjectType.Path,
                        Text = path.IsFilled ? "Filled Vector Shape" : "Stroked Path Line",
                        X = bounds.Left,
                        Y = bounds.Bottom, // Uniform alignment model matching text layout behavior
                        Width = bounds.Width,
                        Height = bounds.Height
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Engine] Geometric vector path structural parse failure.");
            }
        }

    }
}
