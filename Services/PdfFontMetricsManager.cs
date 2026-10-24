using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Content;
using pdf_editor2.Models;

namespace pdf_editor2.Services
{
    /// <summary>
    /// Thread-safe factory service responsible for resolving, caching, 
    /// and normalizing shared flyweight PDF font metric descriptors.
    /// </summary>
    public class PdfFontMetricsManager
    {
        private readonly ILogger<PdfFontMetricsManager> _logger;

        // Global persistent cache map tracking unique font styles across the open document session
        private readonly Dictionary<string, PdfFontMetrics> _globalFontCache = new Dictionary<string, PdfFontMetrics>(StringComparer.OrdinalIgnoreCase);

        public PdfFontMetricsManager(ILogger<PdfFontMetricsManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Flushes all cached font allocations from memory. Invoke this when switching document files.
        /// </summary>
        public void ClearCache()
        {
            lock (_globalFontCache)
            {
                _globalFontCache.Clear();
                _logger.LogDebug("[MetricsManager] Global flyweight font metrics cache cleared successfully.");
            }
        }

        /// <summary>
        /// Resolves a shared flyweight font metrics container from cache, or initializes a new one from the raw PDF stream.
        /// </summary>
        public PdfFontMetrics GetOrCreateFontMetrics(string rawPostScriptName, Letter? firstLetter)
        {
            lock (_globalFontCache)
            {
                if (_globalFontCache.TryGetValue(rawPostScriptName, out var cachedMetrics))
                {
                    return cachedMetrics;
                }

                var sharedMetrics = new PdfFontMetrics
                {
                    RawPostScriptName = rawPostScriptName,
                    FontName = rawPostScriptName,
                    FontFamily = rawPostScriptName,
                    FontWeight = 400,
                    IsBold = false,
                    IsItalic = false,

                    // 🎯 TYPOGRAPHIC ESTIMATION FALLBACKS:
                    // Since low-level PDF FontDescriptors are internal in PdfPig, we map standard typographic baselines
                    // scaled relative to the 1000-unit glyph space. These are automatically proportioned by FontSize downstream.
                    FontAscent = 750,
                    FontDescent = -250,
                    FontCapHeight = 700,
                    FontFlags = 0
                };

                // Extract style traits from the publicly accessible FontDetails property structure
                if (firstLetter?.FontDetails != null)
                {
                    var fontDetails = firstLetter.FontDetails;

                    sharedMetrics.IsBold = fontDetails.IsBold;
                    sharedMetrics.IsItalic = fontDetails.IsItalic;
                }

                NormalizeFontNamesAndWeights(sharedMetrics, rawPostScriptName);

                _globalFontCache[rawPostScriptName] = sharedMetrics;
                _logger.LogDebug("[MetricsManager] Cached new global font profile entry: {Key}", rawPostScriptName);

                return sharedMetrics;
            }
        }

        private void NormalizeFontNamesAndWeights(PdfFontMetrics sharedMetrics, string rawPostScriptName)
        {
            string cleanFontName = rawPostScriptName;
            if (!string.IsNullOrEmpty(cleanFontName))
            {
                string lowerFont = cleanFontName.ToLowerInvariant();
                if (lowerFont.Contains("bold") || lowerFont.Contains("w7") || lowerFont.Contains("-bd")) sharedMetrics.IsBold = true;
                if (lowerFont.Contains("italic") || lowerFont.Contains("oblique") || lowerFont.Contains("-it")) sharedMetrics.IsItalic = true;

                // Strip out custom embedded subset prefixes (e.g., "AAAAAA+Arial" -> "Arial")
                int plusIndex = cleanFontName.IndexOf('+');
                if (plusIndex >= 0 && plusIndex < cleanFontName.Length - 1)
                {
                    cleanFontName = cleanFontName.Substring(plusIndex + 1);
                }

                // Strip out styling suffixes appended past hyphens (e.g., "Arial-Bold" -> "Arial")
                int hyphenIndex = cleanFontName.IndexOf('-');
                if (hyphenIndex > 0)
                {
                    cleanFontName = cleanFontName.Substring(0, hyphenIndex);
                }

                sharedMetrics.FontName = cleanFontName;
                sharedMetrics.FontFamily = cleanFontName;
            }

            // Enforce heavy fallback rule for implicit bold indicators lacking numeric weights
            if (sharedMetrics.FontWeight <= 400 && sharedMetrics.IsBold)
            {
                sharedMetrics.FontWeight = 700;
            }
        }
    }
}
