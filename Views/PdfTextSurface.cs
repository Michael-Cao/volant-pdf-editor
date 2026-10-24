using pdf_editor2.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace pdf_editor2.Views
{
    /// <summary>
    /// A high-performance native WPF surface that renders hundreds of individual words 
    /// directly via low-level DrawingVisual contexts. It implements layout overrides to 
    /// explicitly notify parent Viewbox containers of its native 1:1 asset resolution dimensions.
    /// </summary>
    public class PdfTextSurface : FrameworkElement
    {
        private readonly VisualCollection _visuals;
        private readonly DrawingVisual _drawingVisual;
        private readonly Dictionary<byte[], BitmapImage> _uiImageCache =
              new Dictionary<byte[], BitmapImage>();

        public PdfTextSurface()
        {
            _visuals = new VisualCollection(this);
            _drawingVisual = new DrawingVisual();
            _visuals.Add(_drawingVisual);
        }

        // 1. Data Context source for the extracted word collection
        public static readonly DependencyProperty WordsSourceProperty =
            DependencyProperty.Register(nameof(WordsSource), typeof(IEnumerable<PdfWordData>), typeof(PdfTextSurface),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnWordsSourceChanged));

        public IEnumerable<PdfWordData> WordsSource
        {
            get => (IEnumerable<PdfWordData>)GetValue(WordsSourceProperty);
            set => SetValue(WordsSourceProperty, value);
        }

        // 2. 🚀 Native unscaled Page Width tracking property to guide Viewbox scale calculations
        public static readonly DependencyProperty PageWidthProperty =
            DependencyProperty.Register(nameof(PageWidth), typeof(double), typeof(PdfTextSurface),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double PageWidth
        {
            get => (double)GetValue(PageWidthProperty);
            set => SetValue(PageWidthProperty, value);
        }

        // 3. 🚀 Native unscaled Page Height tracking property to guide Viewbox scale calculations
        public static readonly DependencyProperty PageHeightProperty =
            DependencyProperty.Register(nameof(PageHeight), typeof(double), typeof(PdfTextSurface),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double PageHeight
        {
            get => (double)GetValue(PageHeightProperty);
            set => SetValue(PageHeightProperty, value);
        }

        private static void OnWordsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PdfTextSurface surface)
            {
                surface.RedrawPrimitives(surface.ActualHeight);
            }
        }

        // 🚀 4. MEASURE OVERRIDE: Tells the layout engine the exact 1:1 base pixel footprint dimensions of this surface.
        // This forces the parent Viewbox container to trigger hardware-accelerated magnification scaling.
        protected override Size MeasureOverride(Size availableSize)
        {
            if (PageWidth > 0 && PageHeight > 0)
            {
                return new Size(PageWidth, PageHeight);
            }
            return new Size(708, 888); // Fallback production safety baseline metrics
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (PageWidth > 0 && PageHeight > 0)
            {
                return new Size(PageWidth, PageHeight);
            }
            return new Size(708, 888);
        }

        // 5. High-performance clean 1:1 vector render pass context execution
        private void RedrawPrimitives(double canvasHeightWpfPixels)
        {
            using (System.Windows.Media.DrawingContext dc = _drawingVisual.RenderOpen())
            {
                if (WordsSource == null) return;

                const double pointToPixel = 96.0 / 72.0;
                double pixelsPerDip = System.Windows.Media.VisualTreeHelper.GetDpi(_drawingVisual).PixelsPerDip;
                var brushConverter = new System.Windows.Media.BrushConverter();

                // Instantiate and freeze visual styles outside loop boundaries
                System.Windows.Media.Brush defaultBlackBrush = System.Windows.Media.Brushes.Black;
                System.Windows.Media.Brush imageFill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 0, 122, 204));
                System.Windows.Media.Pen imagePen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.DarkGray, 1);
                System.Windows.Media.Pen pathPen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.DodgerBlue, 1) { DashStyle = System.Windows.Media.DashStyles.Dash };

                imageFill.Freeze();
                imagePen.Freeze();
                pathPen.Freeze();

                foreach (var item in WordsSource)
                {
                    double drawX = item.X * pointToPixel;
                    double drawY = item.Y * pointToPixel;
                    double width = item.Width * pointToPixel;
                    double height = item.Height * pointToPixel;

                    // Invert the layout flow to transform space boundaries
                    double correctedY = canvasHeightWpfPixels - drawY - height;

                    switch (item.ObjectType)
                    {
                        case PdfObjectType.Text:
                            RenderTextPrimitive(dc, item, drawX, correctedY, pointToPixel, defaultBlackBrush, brushConverter, pixelsPerDip);
                            break;

                        case PdfObjectType.Image:
                            RenderImagePrimitive(dc, item, drawX, correctedY, width, height, imageFill, imagePen);
                            break;

                        case PdfObjectType.Path:
                            RenderPathPrimitive(dc, drawX, correctedY, width, height, pathPen);
                            break;
                    }
                }
            }
        }

        private void RenderTextPrimitive(
    System.Windows.Media.DrawingContext dc,
    PdfWordData item,
    double drawX,
    double correctedY,
    double pointToPixel,
    System.Windows.Media.Brush defaultBlackBrush,
    System.Windows.Media.BrushConverter brushConverter,
    double pixelsPerDip)
        {
            if (string.IsNullOrEmpty(item.Text)) return;

            System.Windows.FontStyle style = System.Windows.FontStyles.Normal;
            System.Windows.FontWeight weight = System.Windows.FontWeights.Normal;
            string targetFontFamily = "Georgia";

            // Extract typographic values from flyweight metrics container
            if (item.StyleMetrics != null)
            {
                style = item.StyleMetrics.IsItalic ? System.Windows.FontStyles.Italic : System.Windows.FontStyles.Normal;
                weight = System.Windows.FontWeight.FromOpenTypeWeight(item.StyleMetrics.FontWeight);

                if (!string.IsNullOrEmpty(item.StyleMetrics.FontName))
                {
                    string rawFont = item.StyleMetrics.FontName.ToLowerInvariant();
                    if (rawFont.Contains("sans") || rawFont.Contains("helvetica") || rawFont.Contains("arial"))
                        targetFontFamily = "Segoe UI";
                    else if (rawFont.Contains("courier") || rawFont.Contains("mono") || rawFont.Contains("code"))
                        targetFontFamily = "Consolas";
                    else if (rawFont.Contains("times") || rawFont.Contains("garamond") || rawFont.Contains("serif") || rawFont.Contains("stone"))
                        targetFontFamily = "Times New Roman";
                }
            }

            System.Windows.Media.Brush foregroundBrush = defaultBlackBrush;
            if (!string.IsNullOrEmpty(item.ForegroundColor))
            {
                try { foregroundBrush = (System.Windows.Media.Brush)brushConverter.ConvertFromString(item.ForegroundColor) ?? defaultBlackBrush; }
                catch { /* Secure fallback */ }
            }

            System.Windows.Media.FormattedText formattedText = new System.Windows.Media.FormattedText(
                item.Text, System.Globalization.CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily(targetFontFamily), style, weight, System.Windows.FontStretches.Normal),
                item.FontSize * pointToPixel * 8, foregroundBrush, pixelsPerDip
            );

            // Apply baseline matching calculations
            double rowHeightGridSize = 4.0 * pointToPixel;
            correctedY = Math.Round(correctedY / rowHeightGridSize) * rowHeightGridSize;
            correctedY -= 36.0;

            dc.DrawText(formattedText, new System.Windows.Point(drawX, correctedY));
        }

        private void RenderImagePrimitive(
            System.Windows.Media.DrawingContext dc,
            PdfWordData item,
            double drawX,
            double correctedY,
            double width,
            double height,
            System.Windows.Media.Brush imageFill,
            System.Windows.Media.Pen imagePen)
        {
            System.Windows.Rect rect = new System.Windows.Rect(drawX, correctedY, width, height);
            System.Windows.Media.Imaging.BitmapImage? wpfVisualImage = null;

            if (item.ImageDataPayload != null && item.ImageDataPayload.Length > 0)
            {
                if (_uiImageCache.TryGetValue(item.ImageDataPayload, out var cachedImg))
                {
                    wpfVisualImage = cachedImg;
                }
                else
                {
                    try
                    {
                        using (var ms = new System.IO.MemoryStream(item.ImageDataPayload))
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = ms;
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();

                            _uiImageCache[item.ImageDataPayload] = bitmap;
                            wpfVisualImage = bitmap;
                        }
                    }
                    catch
                    {
                        /* Fallback gracefully */
                    }
                }
            }

            if (wpfVisualImage != null)
            {
                dc.DrawImage(wpfVisualImage, rect);
            }
            else
            {
                dc.DrawRectangle(imageFill, imagePen, rect);
            }
        }

        private void RenderPathPrimitive(
            System.Windows.Media.DrawingContext dc,
            double drawX,
            double correctedY,
            double width,
            double height,
            System.Windows.Media.Pen pathPen)
        {
            System.Windows.Rect rect = new System.Windows.Rect(drawX, correctedY, width, height);
            dc.DrawRectangle(null, pathPen, rect);
        }


        protected override int VisualChildrenCount => _visuals.Count;
        protected override Visual GetVisualChild(int index) => _visuals[index];
    }
}
