using pdf_editor2.Models;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System;
using System.Collections.Generic;
using System.Windows;

namespace pdf_editor2.Views
{
    public class SkiaPdfTextSurface : SKElement
    {
        public static readonly DependencyProperty WordsSourceProperty =
            DependencyProperty.Register(nameof(WordsSource), typeof(IEnumerable<object>), typeof(SkiaPdfTextSurface),
                new PropertyMetadata(null, OnWordsSourceChanged));

        public IEnumerable<object> WordsSource
        {
            get => (IEnumerable<object>)GetValue(WordsSourceProperty);
            set => SetValue(WordsSourceProperty, value);
        }

        private static void OnWordsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkiaPdfTextSurface surface) surface.InvalidateVisual();
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);
            SKCanvas canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (WordsSource == null) return;

            // Calculate and apply physical hardware display DPI scaling factors
            float scaleX = (float)e.Info.Width / (float)ActualWidth;
            float scaleY = (float)e.Info.Height / (float)ActualHeight;

            if (float.IsNaN(scaleX) || float.IsInfinity(scaleX) || scaleX <= 0) scaleX = 1.0f;
            if (float.IsNaN(scaleY) || float.IsInfinity(scaleY) || scaleY <= 0) scaleY = 1.0f;

            canvas.Scale(scaleX, scaleY);

            // Working layout multiplier mapping coordinates from 72 DPI points to 96 DPI space
            float layoutScaleMultiplier = 1.3333333f;

            // 🛠️ INCREASED FONT SCALE MODIFIER: Stepped up from 5.5f to 10.5f.
            // This safely scales up individual character glyph dimensions to fill out lines 
            // completely while keeping the tracking coordinates locked to prevent layout shifting.
            float fontScaleAdjustment = 9.0f;

            using (var textPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill })
            using (var linePaint = new SKPaint { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f })
            using (var fillPaint = new SKPaint { Color = SKColors.Gray, IsAntialias = true, Style = SKPaintStyle.Fill })
            {
                foreach (var item in WordsSource)
                {
                    if (item is not PdfWordData word) continue;

                    // Spatial tracking coordinate calculations remain completely locked and untouched
                    float x = (float)word.X * layoutScaleMultiplier;
                    float y = (float)ActualHeight - ((float)word.Y * layoutScaleMultiplier);

                    if (word.ObjectType == PdfObjectType.Path)
                    {
                        float w = (float)word.Width * layoutScaleMultiplier;
                        float h = (float)word.Height * layoutScaleMultiplier;
                        DrawVectorPath(canvas, word, x, y, w, h, linePaint, fillPaint);
                    }
                    else if (word.ObjectType == PdfObjectType.Text)
                    {
                        textPaint.Color = ParseHexColor(word.ForegroundColor);

                        using (SKTypeface typeface = ResolveTypeface(word.StyleMetrics))
                        using (SKFont font = new SKFont(typeface, 12f))
                        {
                            // Pass down the increased sizing modifier to scale text thickness uniformly
                            DrawTypography(canvas, word, x, y, layoutScaleMultiplier, fontScaleAdjustment, font, textPaint);
                        }
                    }
                }
            }
        }

        private void DrawVectorPath(SKCanvas canvas, PdfWordData path, float x, float y, float w, float h, SKPaint linePaint, SKPaint fillPaint)
        {
            if (path.Text != null && path.Text.Contains("Filled"))
            {
                canvas.DrawRect(new SKRect(x, y - h, x + w, y), fillPaint);
            }
            else
            {
                canvas.DrawLine(x, y, x + w, y, linePaint);
            }
        }

        private void DrawTypography(SKCanvas canvas, PdfWordData word, float x, float y, float layoutMultiplier, float fontAdjustment, SKFont font, SKPaint paint)
        {
            if (string.IsNullOrEmpty(word.Text)) return;

            // Apply size modification parameters strictly inside the scoped typography metrics
            float rawSize = word.FontSize > 0 ? (float)word.FontSize : 12f;
            font.Size = rawSize * layoutMultiplier * fontAdjustment;

            canvas.Save();
            canvas.Translate(x, y);
            canvas.DrawText(word.Text, 0f, 0f, font, paint);
            canvas.Restore();
        }

        private SKTypeface ResolveTypeface(PdfFontMetrics? metrics)
        {
            if (metrics == null) return SKTypeface.FromFamilyName("Arial");

            string pdfFontName = metrics.FontName ?? string.Empty;
            if (string.IsNullOrEmpty(pdfFontName)) return SKTypeface.FromFamilyName("Arial");

            int plusIndex = pdfFontName.IndexOf('+');
            if (plusIndex >= 0 && plusIndex < pdfFontName.Length - 1)
            {
                pdfFontName = pdfFontName.Substring(plusIndex + 1);
            }

            if (pdfFontName.Contains("Times") || pdfFontName.Contains("Georgia") || pdfFontName.Contains("Serif"))
            {
                return SKTypeface.FromFamilyName("Times New Roman");
            }
            if (pdfFontName.Contains("Courier") || pdfFontName.Contains("Mono"))
            {
                return SKTypeface.FromFamilyName("Courier New");
            }
            if (pdfFontName.Contains("Italic", StringComparison.OrdinalIgnoreCase))
            {
                return SKTypeface.FromFamilyName("Arial", SKFontStyle.Italic);
            }
            if (pdfFontName.Contains("Bold", StringComparison.OrdinalIgnoreCase))
            {
                return SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
            }

            return SKTypeface.FromFamilyName(pdfFontName) ?? SKTypeface.FromFamilyName("Arial");
        }

        private SKColor ParseHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return SKColors.Black;
            try
            {
                string cleanHex = hex.Replace("#", "").Trim();
                if (cleanHex.Length == 6) cleanHex = "FF" + cleanHex;
                uint colorValue = Convert.ToUInt32(cleanHex, 16);
                return new SKColor(colorValue);
            }
            catch
            {
                return SKColors.Black;
            }
        }
    }
}
