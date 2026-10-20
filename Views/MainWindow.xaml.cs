using Microsoft.Win32;
using pdf_editor2.Models;
using pdf_editor2.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Security.AccessControl;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace pdf_editor2.Views
{
    public partial class MainWindow : Window
    {
        // ⚡ Decoupled Service Layer for memory-optimized streaming & rendering
        private readonly PdfService _pdfService = new PdfService();
        private ObservableCollection<PdfPageViewModel> _activePages = new ObservableCollection<PdfPageViewModel>();

        private int _totalDocumentPages = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.OemPlus:
                    case Key.Add:
                        e.Handled = true;
                        ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 0.1);
                        break;

                    case Key.OemMinus:
                    case Key.Subtract:
                        e.Handled = true;
                        ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.1);
                        break;

                    case Key.D0:
                    case Key.NumPad0:
                        e.Handled = true;
                        ZoomSlider.Value = 1.0;
                        break;
                }
            }
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                if (e.Delta > 0)
                {
                    ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 0.1);
                }
                else if (e.Delta < 0)
                {
                    ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.1);
                }
            }
        }

        private async void OpenPdf_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "PDF Files (*.pdf)|*.pdf" };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                this.Title = $"PDF Editor - {selectedFilePath}";
                PdfWorkspace.TxtFilePath.Text = $"Active File:\n{selectedFilePath}";

                _totalDocumentPages = 0;
                UpdatePageStatusLabel(0);
                TxtStatusMessage.Text = "Opening document and preparing background skeleton streaming...";

                _activePages.Clear();
                PdfWorkspace.PagesControl.ItemsSource = _activePages;

                // ⚡ Step 1: Rapid Skeleton Extraction. Grabs sizes instantly without blocking UI thread.
                var skeletons = _pdfService.InitializeSkeleton(selectedFilePath);
                _totalDocumentPages = skeletons.Count;

                foreach (var pageSkeleton in skeletons)
                {
                    _activePages.Add(pageSkeleton);
                }

                TxtStatusMessage.Text = $"Loaded {_totalDocumentPages} page placeholder skeletons. Rendering visible viewports...";

                // ⚡ Step 2: Explicitly force an initial viewport check after loading items into the collection
                await ProcessViewportLoadingAsync();
            }
        }

        private async void SavePdf_Click(object sender, RoutedEventArgs e)
        {
            if (_activePages.Count == 0)
            {
                MessageBox.Show("No active document loaded to save.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog { Filter = "PDF Files (*.pdf)|*.pdf" };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    TxtStatusMessage.Text = "Compiling vector data structures into native PDF blocks...";

                    // Execute the background saving operation
                    await _pdfService.SaveDocumentAsync(saveFileDialog.FileName, _activePages);

                    TxtStatusMessage.Text = "Document saved successfully as a searchable, vector PDF.";
                    MessageBox.Show("File exported clean without image flattening!", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    TxtStatusMessage.Text = "Save pipeline aborted due to file locks or engine syntax errors.";
                    MessageBox.Show($"Export error encountered: {ex.Message}", "Export Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClosePdf_Click(object sender, RoutedEventArgs e)
        {
            // Clear page states explicitly to drop heavy unmanaged memory streams instantly
            foreach (var page in _activePages)
            {
                _pdfService.UnloadPageFromMemory(page);
            }

            _activePages.Clear();
            PdfWorkspace.PagesControl.ItemsSource = null;
            _totalDocumentPages = 0;
            UpdatePageStatusLabel(0);

            TxtStatusMessage.Text = "Ready";
            this.Title = "Nova PDF Editor";
            PdfWorkspace.TxtFilePath.Text = "No file loaded";

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PdfWorkspace.WorkspaceScaleTransform == null || TxtZoomStatus == null) return;

            PdfWorkspace.WorkspaceScaleTransform.ScaleX = e.NewValue;
            PdfWorkspace.WorkspaceScaleTransform.ScaleY = e.NewValue;
            TxtZoomStatus.Text = $"{Math.Round(e.NewValue * 100)}%";
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.1);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 0.1);
        }

        // ⚡ DUAL-PURPOSE VIEWPORT SCROLLER & MEMORY MANAGER
        private async void WorkspaceScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_activePages.Count == 0 || _totalDocumentPages == 0) return;

            try
            {
                // PART A: PAGE TRACKING (FIXED AND FINISHED HIT TEST BLOCK)
                Point testPoint = new Point(PdfWorkspace.WorkspaceScrollViewer.ActualWidth / 2, 40);
                HitTestResult hitResult = VisualTreeHelper.HitTest(PdfWorkspace.WorkspaceScrollViewer, testPoint);

                if (hitResult != null && hitResult.VisualHit != null)
                {
                    DependencyObject obj = hitResult.VisualHit;

                    while (obj != null && obj != PdfWorkspace.WorkspaceScrollViewer)
                    {
                        if (obj is FrameworkElement element && element.DataContext is PdfPageViewModel pageModel)
                        {
                            int detectedPageNum = pageModel.PageNumber;
                            int clampedPageNum = Math.Clamp(detectedPageNum, 1, _totalDocumentPages);

                            // Native framework layout helper invocation
                            UpdatePageStatusLabel(clampedPageNum);
                            break;
                        }
                        obj = VisualTreeHelper.GetParent(obj);
                    }
                }

                // PART B: MEMORY MANAGER (ON-DEMAND RENDERING & ACTIVE RECYCLING)
                await ProcessViewportLoadingAsync();
            }
            catch { /* Maintain structural thread stability rules safely */ }
        }

        /// <summary>
        /// Scans the vertical viewport boundary, triggering asynchronous background renders 
        /// for visible pages, while immediately purging off-screen page allocations.
        /// </summary>
        private async Task ProcessViewportLoadingAsync()
        {
            double currentScrollTop = PdfWorkspace.WorkspaceScrollViewer.VerticalOffset;
            double currentViewportHeight = PdfWorkspace.WorkspaceScrollViewer.ViewportHeight;
            double currentScrollBottom = currentScrollTop + currentViewportHeight;

            // Get current scale factor from your transformation UI component
            double currentZoomFactor = PdfWorkspace.WorkspaceScaleTransform?.ScaleY ?? 1.0;
            double accumulatedHeight = 0;

            foreach (PdfPageViewModel page in _activePages)
            {
                // Calculate actual UI scale metrics factoring in the slider's zoom value
                double scaledPageHeight = (page.Height * currentZoomFactor);

                // Assuming an item margin inside your XAML template. Adjust the '20' if your Margin varies.
                double itemMarginCompensation = 20 * currentZoomFactor;

                double pageTopOffset = accumulatedHeight;
                double pageBottomOffset = pageTopOffset + scaledPageHeight + itemMarginCompensation;
                accumulatedHeight = pageBottomOffset;

                // Matrix intersection formula evaluating if the page block intercepts viewable scopes
                bool isPageCurrentlyVisible = (pageTopOffset < currentScrollBottom) && (pageBottomOffset > currentScrollTop);

                if (isPageCurrentlyVisible)
                {
                    // Viewport entrance hook: Render on-demand asynchronously if skipped or unloaded
                    if (!page.IsRendered)
                    {
                        await _pdfService.RenderPageBackgroundAsync(page);
                    }
                }
                else
                {
                    // Viewport exit hook: Clean background textures and layout strings out of RAM instantly
                    if (page.IsRendered)
                    {
                        _pdfService.UnloadPageFromMemory(page);
                    }
                }
            }
        }

        private void UpdatePageStatusLabel(int currentPage)
        {
            TxtPageStatus.Text = $"Page {currentPage} of {_totalDocumentPages}";
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void About_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Nova PDF Editor.");
    }
}
