using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using pdf_editor2.Models;

namespace pdf_editor2.Services
{
    /// <summary>
    /// Dedicated compilation engine for processing text layouts into optimized, unmanaged vector graphics.
    /// Isolates layout iteration loops away from both document lifecycle managers and the main UI thread.
    /// </summary>
    public static class PdfTextCompiler
    {
        // Thread-safe cache pool safeguarding against redundant font parsing and thread-allocation spikes
        private static readonly ConcurrentDictionary<string, SKTypeface> _typefaceCache = new ConcurrentDictionary<string, SKTypeface>();

        /// <summary>
        /// Compiles the structured text elements of a page into a fast, immutable vector recording picture.
        /// Bypasses live iteration locks on the UI layout thread entirely during real-time view scaling.
        /// </summary>
        public static SKPicture CompilePageText(IEnumerable<PdfWordData> wordsData, double pageWidth, double pageHeight)
        {
            using (var recorder = new SKPictureRecorder())
            using (var canvas = recorder.BeginRecording(SKRect.Create(0, 0, (float)pageWidth, (float)pageHeight)))
            using (var paint = new SKPaint { IsAntialias = true })
            {
                if (wordsData != null)
                {
                    foreach (var item in wordsData)
                    {
                        if (string.IsNullOrEmpty(item.Text)) continue;

                        SKTypeface typeface = null;

                        // ✅ FIX: Extract the font descriptor lookup key from the nested StyleMetrics flyweight container
                        string fontKey = item.StyleMetrics?.FontName ?? "DefaultGeorgia";

                        // Attempt to resolve typeface configurations via the shared memory pool registry
                        if (!_typefaceCache.TryGetValue(fontKey, out typeface))
                        {
                            string targetFontFamily = "Georgia";
                            string rawFont = fontKey.ToLowerInvariant();

                            if (rawFont.Contains("sans") || rawFont.Contains("helvetica") || rawFont.Contains("arial"))
                                targetFontFamily = "Segoe UI";
                            else if (rawFont.Contains("courier") || rawFont.Contains("mono") || rawFont.Contains("code"))
                                targetFontFamily = "Consolas";
                            else if (rawFont.Contains("times") || rawFont.Contains("garamond") || rawFont.Contains("serif"))
                                targetFontFamily = "Times New Roman";

                            // ✅ FIX: Resolve font traits through the shared StyleMetrics container
                            bool isBold = item.StyleMetrics?.IsBold ?? false;
                            bool isItalic = item.StyleMetrics?.IsItalic ?? false;

                            SKFontStyleWeight weight = isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
                            SKFontStyleSlant slant = isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

                            typeface = SKTypeface.FromFamilyName(targetFontFamily, weight, SKFontStyleWidth.Normal, slant);

                            if (typeface != null)
                            {
                                _typefaceCache.TryAdd(fontKey, typeface);
                            }
                        }

                        // Apply the parsed text color context or fallback to standard black
                        SKColor parsedColor = SKColors.Black;
                        if (!string.IsNullOrEmpty(item.ForegroundColor) && SKColor.TryParse(item.ForegroundColor, out parsedColor))
                        {
                            paint.Color = parsedColor;
                        }
                        else
                        {
                            paint.Color = SKColors.Black;
                        }

                        // Initialize layout scaling factors (Converting standard 72 Point PDF coordinates to 96 DPI system display vectors)
                        using (var font = new SKFont(typeface, (float)(item.FontSize * 1.33333)))
                        {
                            font.Hinting = SKFontHinting.Normal;
                            font.Edging = SKFontEdging.SubpixelAntialias;

                            float drawX = (float)item.X;
                            float drawY = (float)(pageHeight - item.Y);

                            canvas.DrawText(item.Text, drawX, drawY, font, paint);
                        }
                    }
                }

                return recorder.EndRecording();
            }
        }
    }
}
