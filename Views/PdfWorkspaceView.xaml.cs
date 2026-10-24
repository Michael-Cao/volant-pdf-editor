using Microsoft.Extensions.Logging;
using pdf_editor2.Models;
using pdf_editor2.Services;
using pdf_editor2.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace pdf_editor2.Views
{
    /// <summary>
    /// Interaction logic for PdfWorkspaceView.xaml
    /// </summary>
    public partial class PdfWorkspaceView : UserControl
    {
        private readonly ILogger<PdfWorkspaceView> _logger;
        private readonly PdfService _pdfService;
        private CancellationTokenSource _renderCts;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        public PdfWorkspaceView(PdfService pdfService, ILogger<PdfWorkspaceView> logger)
        {
            _pdfService = pdfService;
            _logger = logger;
            InitializeComponent();

            // Initialize a 150ms debounce timer
            this.DataContextChanged += PdfWorkspaceView_DataContextChanged;

            this.PreviewKeyDown += PdfWorkspaceView_PreviewKeyDown;
        }

        private void PdfWorkspaceView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from old ViewModel if it exists to prevent memory leaks
            //if (e.OldValue is PdfWorkspaceViewModel oldVm)
            //{
            //    oldVm.ActivePages.CollectionChanged -= ActivePages_CollectionChanged;
            //}

            //// Subscribe to the collection changes on the new ViewModel
            //if (e.NewValue is PdfWorkspaceViewModel newVm)
            //{
            //    newVm.ActivePages.CollectionChanged += ActivePages_CollectionChanged;
            //}
        }

        /// <summary>
        /// This function handles the ActivePages collection change event natively!
        /// </summary>
        private async void ActivePages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (DataContext is not PdfWorkspaceViewModel viewModel || viewModel.ActivePages.Count == 0)
                return;

            _logger.LogInformation("Active pages changed");
            // Whenever items are added (like loading a new document skeleton),
            // instantly force the viewport loader to process visible pages before the user even scrolls.
            if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Ensure layout calculations are finalized on screen before scanning positions
                this.UpdateLayout();

                double viewableHeight = WorkspaceScrollViewer.ViewportHeight;

                // If the window is still initializing and height isn't ready, fallback to ActualHeight
                if (viewableHeight <= 0) viewableHeight = WorkspaceScrollViewer.ActualHeight;

                // 3. ⚡ Apply the calculated default zoom constraint smoothly!
                viewModel.FitToViewportHeight(viewableHeight);

                // 4. Fire your sliding background rendering context routine safely
                _renderCts?.Cancel();
                _renderCts = new CancellationTokenSource();

                try
                {
                    await ProcessViewportLoadingAsync(_renderCts.Token);
                }
                catch (OperationCanceledException) { }
            }
        }

        private async void WorkspaceScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Safeguard if DataContext is not bound yet
            if (DataContext is not PdfWorkspaceViewModel viewModel || viewModel.ActivePages.Count == 0)
                return;

            try
            {
                // PART A: VISUAL HIT TESTING (Find what page the user is looking at)
                Point testPoint = new Point(WorkspaceScrollViewer.ActualWidth / 2, 40);
                HitTestResult hitResult = VisualTreeHelper.HitTest(WorkspaceScrollViewer, testPoint);

                if (hitResult?.VisualHit != null)
                {
                    DependencyObject obj = hitResult.VisualHit;
                    while (obj != null && obj != WorkspaceScrollViewer)
                    {
                        if (obj is FrameworkElement element && element.DataContext is PdfPageViewModel pageModel)
                        {
                            // Update the Current Page selection on the ViewModel
                            viewModel.CurrentPageNumber = pageModel.PageNumber;
                            break;
                        }
                        obj = VisualTreeHelper.GetParent(obj);
                    }
                }
            }
            catch
            {
                /* Maintain structural thread stability rules safely */
            }
        }

        /// <summary>
        /// Scans the vertical viewport boundary, triggering asynchronous background renders 
        /// for visible pages, while immediately purging off-screen page allocations.
        /// </summary>
        private async Task ProcessViewportLoadingAsync(CancellationToken cancellationToken)
        {
            if (DataContext is not PdfWorkspaceViewModel viewModel || viewModel.ActivePages.Count == 0)
                return;

            // 1. Identify our absolute anchor index (0-based)
            int targetIndex = viewModel.CurrentPageNumber - 1;
            int totalPages = viewModel.ActivePages.Count;

            // 2. Define our strict memory boundary: Current Page + 2 Page Buffer around it
            // This looks at 1 page above and 2 pages ahead, or custom tailored combinations
            int bufferRange = 2;
            int minBufferIndex = Math.Max(0, targetIndex - 1);
            int maxBufferIndex = Math.Min(totalPages - 1, targetIndex + bufferRange);

            // 3. Process allocations across the entire document collection instantly
            for (int i = 0; i < totalPages; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfPageViewModel page = viewModel.ActivePages[i];

                bool shouldBeBuffered = (i >= minBufferIndex && i <= maxBufferIndex);

                if (shouldBeBuffered)
                {
                    if (!page.IsRendered)
                    {
                        // Materialize sequentially checking token safety bounds continuously
                        await _pdfService.MaterializePageUiAsync(page);
                    }
                }
                else
                {
                    // Viewport Exit Hook: Instantly purge out-of-bounds page structures from RAM
                    if (page.IsRendered)
                    {
                        _pdfService.RequestPageUnload(page);
                    }
                }
            }
        }

        /// <summary>
        /// JIT Entrance Hook: Executed ONLY when the page framework structure is physically entering the layout view screen.
        /// </summary>
        private async void PageElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PdfPageViewModel pageModel)
            {
                if (!pageModel.IsRendered)
                {
                    // Call the background materialization routine on demand!
                    // Passing System.Threading.CancellationToken.None or a structured token as needed
                    await _pdfService.MaterializePageUiAsync(pageModel);
                }
            }
        }

        /// <summary>
        /// JIT Exit Hook: Executed ONLY when a page scrolls completely away out of the virtualized screen cache limits.
        /// </summary>
        private void PageElement_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PdfPageViewModel pageModel)
            {
                if (pageModel.IsRendered)
                {
                    // Wipe the heavy images out of RAM instantly
                    _pdfService.RequestPageUnload(pageModel);
                }
            }
        }

        private void PdfWorkspaceView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (WorkspaceScrollViewer == null) return;

            if (e.Key == Key.PageDown)
            {
                e.Handled = true; // Mark as processed

                // Compute new position matching a standard single-frame viewport jump down
                double targetOffset = WorkspaceScrollViewer.VerticalOffset + WorkspaceScrollViewer.ViewportHeight;
                WorkspaceScrollViewer.ScrollToVerticalOffset(Math.Min(WorkspaceScrollViewer.ScrollableHeight, targetOffset));
            }
            else if (e.Key == Key.PageUp)
            {
                e.Handled = true; // Mark as processed

                // Compute new position matching a standard single-frame viewport jump up
                double targetOffset = WorkspaceScrollViewer.VerticalOffset - WorkspaceScrollViewer.ViewportHeight;
                WorkspaceScrollViewer.ScrollToVerticalOffset(Math.Max(0, targetOffset));
            }
        }


        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T t)
                {
                    return t;
                }

                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void SkiaTextSurface_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            // 1. Resolve the active SKElement surface and extract its structural DataContext
            if (sender is SKElement skElement && skElement.DataContext is var pageViewModel)
            {
                // Access the underlying collection of PDF words mapped during parser extraction
                dynamic pageData = pageViewModel;
                var wordsData = pageData.WordsData;

                if (wordsData == null) return;

                SKCanvas canvas = e.Surface.Canvas;

                // Clear surface with total transparency to prevent overlapping structural art layers
                canvas.Clear(SKColors.Transparent);

                // 2. Normalize and bind high-DPI physical pixels to match WPF logical coordinates
                float scaleX = (float)e.Info.Width / (float)skElement.ActualWidth;
                float scaleY = (float)e.Info.Height / (float)skElement.ActualHeight;

                canvas.Save();
                canvas.Scale(scaleX, scaleY);

                // 3. Process the sequential text pipeline glyph by glyph
                foreach (var item in wordsData)
                {
                    if (string.IsNullOrEmpty(item.Text)) continue;

                    SKTypeface typeface = null;

                    // 4. EMBEDDED FONT STREAM RESTORATION (The Ultimate Fix)
                    // If PdfPig captured the raw embedded binary font data subset from the PDF dictionary,
                    // we instantiate it directly inside memory to avoid any system font dependency mutations.
                    if (item.StyleMetrics?.FontData != null && item.StyleMetrics.FontData.Length > 0)
                    {
                        using (var ms = new System.IO.MemoryStream(item.StyleMetrics.FontData))
                        {
                            typeface = SKTypeface.FromStream(ms);
                        }
                    }

                    // 5. SECURE SYSTEM FALLBACK MATCHING
                    // Executed only if the font was not securely embedded inside the PDF resource dictionary.
                    if (typeface == null)
                    {
                        string targetFontFamily = "Georgia";
                        if (item.StyleMetrics != null && !string.IsNullOrEmpty(item.StyleMetrics.FontName))
                        {
                            string rawFont = item.StyleMetrics.FontName.ToLowerInvariant();
                            if (rawFont.Contains("sans") || rawFont.Contains("helvetica") || rawFont.Contains("arial"))
                                targetFontFamily = "Segoe UI";
                            else if (rawFont.Contains("courier") || rawFont.Contains("mono") || rawFont.Contains("code"))
                                targetFontFamily = "Consolas";
                            else if (rawFont.Contains("times") || rawFont.Contains("garamond") || rawFont.Contains("serif"))
                                targetFontFamily = "Times New Roman";
                        }

                        SKFontStyleWeight weight = item.StyleMetrics != null ? (SKFontStyleWeight)item.StyleMetrics.FontWeight : SKFontStyleWeight.Normal;
                        SKFontStyleSlant slant = (item.StyleMetrics != null && item.StyleMetrics.IsItalic) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

                        typeface = SKTypeface.FromFamilyName(targetFontFamily, weight, SKFontStyleWidth.Normal, slant);
                    }

                    // 6. RENDER ENGINE INITIALIZATION & METRIC TRANSFORMATIONS
                    // Conversion factor note: 1.33333 (96 DPI / 72 Points per inch) matches standard PDF-to-Pixel coordinates
                    using (var paint = new SKPaint())
                    using (var font = new SKFont(typeface, (float)(item.FontSize * 1.33333)))
                    {
                        SKColor parsedColor = SKColors.Black;
                        if (!string.IsNullOrEmpty(item.ForegroundColor) && SKColor.TryParse(item.ForegroundColor, out parsedColor))
                        {
                            paint.Color = parsedColor;
                        }
                        else
                        {
                            paint.Color = SKColors.Black;
                        }

                        paint.IsAntialias = true;

                        // Enable hardware-level edge smoothing and typography subpixel tracking
                        paint.IsAntialias = true;
                        font.Hinting = SKFontHinting.Normal;

                        // SKFontEdging.SubpixelAntialias activates both crisp LCD subpixel rendering and text smoothing.
                        font.Edging = SKFontEdging.SubpixelAntialias;

                        // 7. DIRECT ABSOLUTE VECTOR PROJECTION
                        // Map layout vectors natively using the absolute position metadata.
                        // Skia positions characters relative to the typographic baseline (bottom-left).
                        float drawX = (float)item.X;
                        float drawY = (float)item.Y;

                        canvas.DrawText(item.Text, drawX, drawY, font, paint);
                    }
                }

                // Restore state matrix stack tracking parameters
                canvas.Restore();
            }
        }
    }
}
