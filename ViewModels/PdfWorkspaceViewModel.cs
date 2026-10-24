using Microsoft.Extensions.Logging;
using pdf_editor2.Models;
using pdf_editor2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace pdf_editor2.ViewModels
{
    public class PdfWorkspaceViewModel : ViewModelBase
    {
        private readonly ILogger<PdfWorkspaceViewModel> _logger;
        private readonly PdfService _pdfService;
        private string _currentFilePath = string.Empty;

        public ObservableCollection<PdfPageViewModel> ActivePages { get; } = new();
        public ObservableCollection<PdfWordData> ExtractedObjects { get; } = new();

        /// <summary>
        /// Holds a comprehensive, de-duplicated list of all unique text styles used throughout the document.
        /// </summary>
        public ObservableCollection<PdfGlobalStyle> DocumentStylesPalette { get; } = new();

        private List<PdfWordData> _displayObjectsList = new List<PdfWordData>();

        private int _currentPageNumber = 1;
        public int CurrentPageNumber
        {
            get => _currentPageNumber;
            set
            {
                if (value >= 1 && value <= TotalDocumentPages)
                {
                    SetProperty(ref _currentPageNumber, value);

                    // Parse structural stream data when changing pages
                    _logger.LogInformation("Page changed. Refreshing side panel objects for page {PageNum}", _currentPageNumber);
                    RefreshPageObjects();
                }
            }
        }

        private int _totalDocumentPages = 0;
        public int TotalDocumentPages
        {
            get => _totalDocumentPages;
            set => SetProperty(ref _totalDocumentPages, value);
        }


        private double _zoomFactor = 1.0;
        public double ZoomFactor
        {
            get => _zoomFactor;
            set => SetProperty(ref _zoomFactor, value);
        }

        private bool _isStructuredRenderingMode;

        /// <summary>
        /// Gets or sets a value indicating whether the page should render using 
        /// structured layout elements (text, paths, images) instead of a flat bitmap image.
        /// </summary>
        public bool IsStructuredRenderingMode
        {
            get => _isStructuredRenderingMode;
            set
            {
                if (_isStructuredRenderingMode != value)
                {
                    _isStructuredRenderingMode = value;
                    OnPropertyChanged(nameof(IsStructuredRenderingMode));

                    // Trigger a refresh of the page display mode if necessary
                    RefreshPageObjects();
                }
            }
        }

        public PdfWorkspaceViewModel(PdfService pdfService, ILogger<PdfWorkspaceViewModel> logger)
        {
            _pdfService = pdfService;
            _logger = logger;
        }

        /// <summary>
        /// Instantly initializes empty skeletal framework wrappers to populate layout panels.
        /// </summary>
        public async void LoadNewDocument(string filePath)
        {
            _logger.LogInformation("Loading document {FilePath}", filePath);

            // Safety check: instantly cache the current active file path state natively first
            _currentFilePath = filePath;
            ActivePages.Clear();
            DocumentStylesPalette.Clear();

            var skeletons = _pdfService.CreateDocumentSkeleton(filePath);
            TotalDocumentPages = skeletons.Count;

            // Safely reset the page state trackers without firing accidental setup alerts early
            _currentPageNumber = 1;
            OnPropertyChanged(nameof(CurrentPageNumber));

            foreach (var page in skeletons)
            {
                ActivePages.Add(page);
            }
            _logger.LogInformation("Loaded {Count} pages", ActivePages.Count);

            // Initial sidebar inspector populate trigger
            RefreshPageObjects();

            //// 🚀 ASYNC GLOBAL STYLE INDEXING: 
            //// Offload the heavy multi-page character loop to a background task runner thread.
            //// This allows the document pages to load instantly on the user screen without UI lag.
            //try
            //{
            //    _logger.LogInformation("Spawning background thread worker to analyze global text typography maps...");

            //    // Pass your ScanDocumentGlobalStyles proxy endpoint method down to the task thread runner
            //    List<PdfGlobalStyle> masterStyles = await Task.Run(() => _pdfService.ScanDocumentGlobalStyles(filePath));

            //    // Marshal back to the true Main UI thread to populate your dropdown palette safely
            //    System.Windows.Application.Current.Dispatcher.Invoke(() =>
            //    {
            //        foreach (var style in masterStyles)
            //        {
            //            DocumentStylesPalette.Add(style);
            //        }
            //        _logger.LogInformation("Background indexing finished. Successfully loaded {Count} unique styles to UI palette.", DocumentStylesPalette.Count);
            //    });
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, "Failed aggregating global unique text typography styles during document initialization.");
            //}
        }

        /// <summary>
        /// Expose a flat list property that forces the DataGrid to hard-refresh its internal row visual containers.
        /// </summary>
        public List<PdfWordData> DisplayObjectsList
        {
            get => _displayObjectsList;
            private set
            {
                _displayObjectsList = value;
                OnPropertyChanged(nameof(DisplayObjectsList));

                // Keep your old collection populated so your bottom counter binding template does not break
                ExtractedObjects.Clear();
                if (_displayObjectsList != null)
                {
                    foreach (var item in _displayObjectsList)
                    {
                        ExtractedObjects.Add(item);
                    }
                }
                OnPropertyChanged(nameof(ExtractedObjects));
            }
        }


        /// <summary>
        /// Invokes low-level page token scanning routines and safely updates the bound UI data collection.
        /// </summary>
        private void RefreshPageObjects()
        {
            int pageIndex = _currentPageNumber - 1;
            if (pageIndex < 0 || pageIndex >= ActivePages.Count)
            {
                ExtractedObjects.Clear();
                return;
            }

            var currentPageViewModel = ActivePages[pageIndex];

            if (currentPageViewModel.IsRendered && currentPageViewModel.WordsData != null)
            {
                _logger.LogInformation("[UI-Sync] Converting {Count} word tokens into target view primitives.", currentPageViewModel.WordsData.Count);

                ExtractedObjects.Clear();

                var elementsToPush = new List<pdf_editor2.Views.SideViews.PdfPageElement>();
                int currentId = 1;

                foreach (var word in currentPageViewModel.WordsData)
                {
                    // FIX: Convert the ObjectType enum into clean display names for your columns
                    string displayType = word.ObjectType switch
                    {
                        PdfObjectType.Image => "Image",
                        PdfObjectType.Path => "Path",
                        _ => "Paragraph"
                    };

                    elementsToPush.Add(new pdf_editor2.Views.SideViews.PdfPageElement
                    {
                        ElementId = currentId++,
                        ElementType = displayType, // Maps dynamically to "Text", "Image", or "Path" strings
                        Details = word.Text,
                        BoundingBox = $"[{Math.Round(word.X, 1)}, {Math.Round(word.Y, 1)}, {Math.Round(word.Width, 1)}x{Math.Round(word.Height, 1)}]"
                    });

                    ExtractedObjects.Add(word);
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow as pdf_editor2.Views.MainWindow;
                    if (mainWindow != null)
                    {
                        var inspectorView = FindVisualChild<pdf_editor2.Views.SideViews.ObjectInspectorView>(mainWindow);
                        if (inspectorView != null)
                        {
                            inspectorView.UpdatePageElements(elementsToPush);
                        }
                    }
                });

                OnPropertyChanged(nameof(ExtractedObjects));
            }
        }

        /// <summary>
        /// Helper utility method to locate deeply nested user controls down the WPF layout tree.
        /// </summary>
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            // Scan down through all active immediate visual layers sequentially
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T target) return target;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        /// <summary>
        /// Evaluates viewport boundaries to dynamically load/unload heavy page textures.
        /// </summary>
        public async Task ProcessViewportLoadingAsync(double scrollTop, double viewportHeight, double currentZoomFactor)
        {
            double scrollBottom = scrollTop + viewportHeight;
            double accumulatedHeight = 0;

            foreach (PdfPageViewModel page in ActivePages)
            {
                double scaledPageHeight = page.Height * currentZoomFactor;
                double itemMarginCompensation = 20 * currentZoomFactor;

                double pageTopOffset = accumulatedHeight;
                double pageBottomOffset = pageTopOffset + scaledPageHeight + itemMarginCompensation;
                accumulatedHeight = pageBottomOffset;

                bool isPageCurrentlyVisible = (pageTopOffset < scrollBottom) && (pageBottomOffset > scrollTop);

                if (isPageCurrentlyVisible)
                {
                    if (!page.IsRendered)
                    {
                        await _pdfService.MaterializePageUiAsync(page);

                        if (page.PageNumber == CurrentPageNumber)
                        {
                            RefreshPageObjects();
                        }
                    }
                }
                else
                {
                    if (page.IsRendered)
                    {
                        _pdfService.RequestPageUnload(page);
                    }
                }
            }
        }

        public void FitToViewportHeight(double availableViewportHeight)
        {
            if (ActivePages.Count == 0 || availableViewportHeight <= 0) return;

            // 1. Target the first page as our master dimensional metric anchor
            var firstPage = ActivePages[0];

            // 2. Accounts for the layout padding/margin we set in XAML (e.g., 20 units total for margins)
            double marginCompensation = 20;
            double operationalHeight = firstPage.Height + marginCompensation;

            // 3. Compute ratio and assign it directly to our bound ZoomFactor property
            double targetZoom = availableViewportHeight / operationalHeight;

            // Clamp it to reasonable thresholds matching your status bar slider limits (0.5x to 3.0x)
            ZoomFactor = Math.Clamp(targetZoom, 0.5, 3.0);
        }
    }
}
