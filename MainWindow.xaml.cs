using Microsoft.Win32;
using pdf_editor2.Models;
using pdf_editor2.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace pdf_editor2
{
    public partial class MainWindow : Window
    {
        private readonly PdfRenderEngine _renderEngine = new PdfRenderEngine();
        private ObservableCollection<PdfPageViewModel> _activePages = new ObservableCollection<PdfPageViewModel>();

        // Tracks the overall total file capacity count locally
        private int _totalDocumentPages = 0;

        public MainWindow()
        {
            InitializeComponent();
            _renderEngine.OnPageParsed += RenderEngine_OnPageParsed;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Check if the Control key is currently being pressed down
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    // ➕ ZOOM IN SHORTCUTS (Supports standard '=' key, Shifted '+' key, and Numpad Add)
                    case Key.OemPlus:
                    case Key.Add:
                        e.Handled = true; // Prevents the keypress from triggering typing events
                        ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 0.1);
                        break;

                    // ➖ ZOOM OUT SHORTCUTS (Supports standard '-' key and Numpad Subtract)
                    case Key.OemMinus:
                    case Key.Subtract:
                        e.Handled = true;
                        ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.1);
                        break;

                    // 🎯 OPTIONAL: Ctrl + 0 resets zoom back to 100% standard layout
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
            // Check if the Control key is physically held down during the mouse roll event
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Mark the event as handled to stop the ScrollViewer from jumping up/down vertically
                e.Handled = true;

                if (e.Delta > 0)
                {
                    // 🔄 Roll Up -> Zoom In (Increment scale by 10% blocks)
                    ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 0.1);
                }
                else if (e.Delta < 0)
                {
                    // 🔄 Roll Down -> Zoom Out (Decrement scale by 10% blocks)
                    ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.1);
                }
            }
        }

        private void OpenPdf_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "PDF Files (*.pdf)|*.pdf" };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                this.Title = $"PDF Editor - {selectedFilePath}";
                TxtFilePath.Text = $"Active File:\n{selectedFilePath}";

                _totalDocumentPages = 0;
                UpdatePageStatusLabel(0);

                // ⚡ Update Message Pane right as parsing begins
                TxtStatusMessage.Text = "Opening document and preparing background stream engine...";

                _activePages.Clear();
                PagesControl.ItemsSource = _activePages;

                _renderEngine.StartStreamingDocument(selectedFilePath);
            }
        }

        private void RenderEngine_OnPageParsed(object sender, PageParsedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _activePages.Add(e.PageViewModel);
                _totalDocumentPages = e.TotalPages;

                // ⚡ Update Message Pane dynamically as each page streams in
                if (_activePages.Count < _totalDocumentPages)
                {
                    TxtStatusMessage.Text = $"Streaming page {_activePages.Count} of {_totalDocumentPages}... (UI is active)";
                }
                else
                {
                    TxtStatusMessage.Text = "Document loading complete. Ready to edit.";
                }

                if (_activePages.Count == 1 || WorkspaceScrollViewer.VerticalOffset == 0)
                {
                    UpdatePageStatusLabel(1);
                }
            });
        }

        private void ClosePdf_Click(object sender, RoutedEventArgs e)
        {
            _activePages.Clear();
            PagesControl.ItemsSource = null;
            _totalDocumentPages = 0;
            UpdatePageStatusLabel(0);

            // ⚡ Reset message pane on close
            TxtStatusMessage.Text = "Ready";

            this.Title = "Nova PDF Editor";
            TxtFilePath.Text = "No file loaded";

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // ⚡ Triggers smoothly as the slider shifts or buttons tweak values
        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Check initialization boundaries to prevent loading crashes
            if (WorkspaceScaleTransform == null || TxtZoomStatus == null) return;

            // Apply the scaling factors to our workspace layout transformation layer
            WorkspaceScaleTransform.ScaleX = e.NewValue;
            WorkspaceScaleTransform.ScaleY = e.NewValue;

            // Output human-readable percentage metric text (e.g., 1.5 -> "150%")
            TxtZoomStatus.Text = $"{Math.Round(e.NewValue * 100)}%";
        }

        // ➖ Decrements scale layout by 10% blocks
        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.1);
        }

        // ➕ Increments scale layout by 10% blocks
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 0.1);
        }

        // ⚡ ACTIVE SCROLL CALCULATOR: Triggers dynamically whenever the mouse wheel spins
        private void WorkspaceScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_activePages.Count == 0 || _totalDocumentPages == 0) return;

            try
            {
                // 1. Define a target point near the top-center of our ScrollViewer window zone
                // This acts like a virtual scanning laser line checking what page passes underneath it
                Point testPoint = new Point(WorkspaceScrollViewer.ActualWidth / 2, 40);

                // 2. Perform a Hit Test to find what visual element is physically sitting at that point
                HitTestResult hitResult = VisualTreeHelper.HitTest(WorkspaceScrollViewer, testPoint);

                if (hitResult != null && hitResult.VisualHit != null)
                {
                    DependencyObject obj = hitResult.VisualHit;

                    // 3. Walk up the visual layout nesting tree to locate our FrameworkElement page block container
                    while (obj != null && obj != WorkspaceScrollViewer)
                    {
                        // Check if this layout item is currently holding one of our PdfPageViewModel models
                        if (obj is FrameworkElement element && element.DataContext is PdfPageViewModel pageModel)
                        {
                            int detectedPageNum = pageModel.PageNumber;

                            // Constrain the result within valid document bounds and update label
                            int clampedPageNum = Math.Clamp(detectedPageNum, 1, _totalDocumentPages);
                            UpdatePageStatusLabel(clampedPageNum);
                            return;
                        }

                        // Keep climbing up the parent layout tree
                        obj = VisualTreeHelper.GetParent(obj);
                    }
                }
            }
            catch
            {
                // Fallback to basic offset calculation if layout is temporarily measuring/redrawing
                int fallbackIndex = (int)Math.Clamp(Math.Round(WorkspaceScrollViewer.VerticalOffset) + 1, 1, _totalDocumentPages);
                UpdatePageStatusLabel(fallbackIndex);
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
