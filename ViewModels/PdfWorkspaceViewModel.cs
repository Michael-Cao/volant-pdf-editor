using Microsoft.Extensions.Logging;
using pdf_editor2.Models;
using pdf_editor2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace pdf_editor2.ViewModels
{
    public class PdfWorkspaceViewModel : ViewModelBase
    {
        private readonly ILogger<PdfWorkspaceViewModel> _logger;
        private readonly PdfService _pdfService;

        public ObservableCollection<PdfPageViewModel> ActivePages { get; } = new();

        private int _currentPageNumber = 1;
        public int CurrentPageNumber
        {
            get => _currentPageNumber;
            set
            {
                if (value >= 1 && value <= TotalDocumentPages)
                {
                    SetProperty(ref _currentPageNumber, value);
                    // Pro Tip: Broadcast this change out to your PdfStatusBarView here!
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

        public PdfWorkspaceViewModel(PdfService pdfService, ILogger<PdfWorkspaceViewModel> logger)
        {
            _pdfService = pdfService;
            _logger = logger;
        }

        /// <summary>
        /// Instantly initializes empty skeletal framework wrappers to populate layout panels.
        /// </summary>
        public void LoadNewDocument(string filePath)
        {
            _logger.LogInformation("Loading document {FilePath}", filePath);
            ActivePages.Clear();

            var skeletons = _pdfService.CreateDocumentSkeleton(filePath);
            TotalDocumentPages = skeletons.Count;
            CurrentPageNumber = 1;

            foreach (var page in skeletons)
            {
                ActivePages.Add(page);
            }
            _logger.LogInformation("Loaded {Count} pages", ActivePages.Count);
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
