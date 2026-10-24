using System;
using System.Threading;
using System.Threading.Tasks;
using pdf_editor2.Models;
using pdf_editor2.ViewModels;

namespace pdf_editor2.Services
{
    /// <summary>
    /// Static mathematical allocation processor for managing document viewport boundary calculation logic.
    /// Completely disconnected from active WPF control hierarchies to eliminate re-entrancy layout lags.
    /// </summary>
    public static class PdfViewportEngine
    {
        /// <summary>
        /// Evaluates active visible indices and securely dispatches sequential materialization or eviction threads.
        /// </summary>
        public static async Task ProcessViewportRangeAsync(PdfService pdfService, PdfWorkspaceViewModel viewModel, CancellationToken cancellationToken)
        {
            if (pdfService == null || viewModel == null || viewModel.ActivePages.Count == 0) return;

            int targetIndex = viewModel.CurrentPageNumber - 1;
            int totalPages = viewModel.ActivePages.Count;

            // Strict layout buffer constants boundary definition
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
                        // Safely request memory allocations from the backend service engine layer
                        await pdfService.MaterializePageUiAsync(page);
                    }
                }
                else
                {
                    if (page.IsRendered)
                    {
                        // Clean up memory pipelines for offscreen allocations immediately
                        pdfService.RequestPageUnload(page);
                    }
                }
            }
        }
    }
}
