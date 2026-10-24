using Microsoft.Extensions.Logging;
using pdf_editor2.Models;
using pdf_editor2.ViewModels;
using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace pdf_editor2.Services
{
    /// <summary>
    /// Decoupled virtualization manager handling JIT element instantiation, 
    /// viewport boundaries checking, and memory eviction management loops.
    /// </summary>
    public class PdfViewportManager
    {
        private readonly PdfService _pdfService;
        private readonly ILogger _logger;

        private ItemsControl _pagesControl;
        private ScrollViewer _scrollViewer;
        private CancellationTokenSource _renderCts;

        public PdfViewportManager(PdfService pdfService, ILogger logger)
        {
            _pdfService = pdfService;
            _logger = logger;
        }

        public void SetWorkspaceContext(ItemsControl pagesControl, ScrollViewer scrollViewer)
        {
            _pagesControl = pagesControl;
            _scrollViewer = scrollViewer;
        }

        public async void ActivePages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_scrollViewer == null || _pagesControl?.DataContext is not PdfWorkspaceViewModel viewModel || viewModel.ActivePages.Count == 0)
                return;

            _logger.LogInformation("[Viewport Manager] Active document schema collection mutated.");

            if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Reset)
            {
                _pagesControl.UpdateLayout();
                double viewableHeight = _scrollViewer.ViewportHeight <= 0 ? _scrollViewer.ActualHeight : _scrollViewer.ViewportHeight;

                viewModel.FitToViewportHeight(viewableHeight);

                _renderCts?.Cancel();
                _renderCts = new CancellationTokenSource();

                try
                {
                    await ProcessViewportLoadingAsync(viewModel, _renderCts.Token);
                }
                catch (OperationCanceledException) { }
            }
        }

        public void HandleScrollChange(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null || _pagesControl?.DataContext is not PdfWorkspaceViewModel viewModel || viewModel.ActivePages.Count == 0)
                return;

            try
            {
                Point testPoint = new Point(scrollViewer.ActualWidth / 2, 40);
                HitTestResult hitResult = VisualTreeHelper.HitTest(scrollViewer, testPoint);

                if (hitResult?.VisualHit != null)
                {
                    DependencyObject obj = hitResult.VisualHit;
                    while (obj != null && obj != scrollViewer)
                    {
                        if (obj is FrameworkElement element && element.DataContext is PdfPageViewModel pageModel)
                        {
                            viewModel.CurrentPageNumber = pageModel.PageNumber;
                            break;
                        }
                        obj = VisualTreeHelper.GetParent(obj);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Muted safe visualization tracking collision.");
            }
        }

        public async void HandlePageLoaded(FrameworkElement element)
        {
            if (element?.DataContext is PdfPageViewModel pageModel && !pageModel.IsRendered)
            {
                _logger.LogDebug("[Viewport JIT] Page #{PageNum} entered layout frame. Triggering asynchronous load.", pageModel.PageNumber);
                await _pdfService.MaterializePageUiAsync(pageModel);
            }
        }

        public void HandlePageUnloaded(FrameworkElement element)
        {
            if (element?.DataContext is PdfPageViewModel pageModel && pageModel.IsRendered)
            {
                _logger.LogDebug("[Viewport JIT] Page #{PageNum} exited cache boundary. Evicting allocations from RAM.", pageModel.PageNumber);
                _pdfService.RequestPageUnload(pageModel);
            }
        }

        private async Task ProcessViewportLoadingAsync(PdfWorkspaceViewModel viewModel, CancellationToken cancellationToken)
        {
            int targetIndex = viewModel.CurrentPageNumber - 1;
            int totalPages = viewModel.ActivePages.Count;

            int minBufferIndex = Math.Max(0, targetIndex - 1);
            int maxBufferIndex = Math.Min(totalPages - 1, targetIndex + 2);

            for (int i = 0; i < totalPages; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfPageViewModel page = viewModel.ActivePages[i];

                bool shouldBeBuffered = (i >= minBufferIndex && i <= maxBufferIndex);

                if (shouldBeBuffered)
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
    }
}
