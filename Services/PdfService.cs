using Microsoft.Extensions.Logging;
using pdf_editor2.Models;
using pdf_editor2.Services;
using pdf_editor2.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;


public class PdfService
{
    private readonly ILogger<PdfService> _logger;
    private readonly PdfRenderEngine _renderEngine;

    // Inject your engine cleanly via dependency injection
    public PdfService(PdfRenderEngine renderEngine, ILogger<PdfService> logger)
    {
        _renderEngine = renderEngine;
        _logger = logger;
    }

    public async Task RenderPageBackgroundAsync(PdfPageViewModel pageModel)
    {
        if (pageModel.IsRendered) return;

        // Delegate the heavy background lifting entirely to the engine layer
        // We pass the page object along or map it cleanly down via matching keys
        await _renderEngine.RenderAndParsePageAsync(pageModel.FilePath, pageModel.PageNumber);
    }

    public List<PdfPageViewModel> CreateDocumentSkeleton(string filePath)
    {
        _logger.LogInformation("Create document skeleton...");
        var metaDataList = _renderEngine.GetDocumentSkeleton(filePath);

        // Map DTO data structures cleanly directly into UI ViewModels
        return metaDataList.Select(meta => new PdfPageViewModel
        {
            PageNumber = meta.PageNumber,
            Width = meta.Width,
            Height = meta.Height,
            FilePath = meta.FilePath,
            IsRendered = false
        }).ToList();
    }

    public async Task MaterializePageUiAsync(PdfPageViewModel pageViewModel)
    {
        if (pageViewModel.IsRendered) return;

        _logger.LogInformation("Materializing page {Page}...", pageViewModel.PageNumber);

        // 1. Let the engine crunch raw numbers/bytes off the UI thread
        var dataResult = await _renderEngine.RenderAndParsePageAsync(pageViewModel.FilePath, pageViewModel.PageNumber);

        // 2. Marshal back to UI thread cleanly solely for WPF Graphic Context initialization
        Application.Current.Dispatcher.Invoke(() =>
        {
            BitmapImage wpfBitmap = new BitmapImage();
            wpfBitmap.BeginInit();
            wpfBitmap.StreamSource = new MemoryStream(dataResult.RawPngBytes);
            wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
            wpfBitmap.EndInit();
            wpfBitmap.Freeze(); // Freeze so WPF can handle thread-neutral consumption

            // Update UI bound data safely
            pageViewModel.WordsData = dataResult.WordsData;
            pageViewModel.BackgroundImage = wpfBitmap;
            pageViewModel.IsRendered = true;
        });
    }

    public void RequestPageUnload(PdfPageViewModel pageViewModel)
    {
        if (!pageViewModel.IsRendered) return;

        // Clear UI presentation layers safely
        pageViewModel.BackgroundImage = null;
        pageViewModel.WordsData = null;
        pageViewModel.PageStructure = null;
        pageViewModel.IsRendered = false;

        // Instruct engine to drop back-end buffer arrays if required
        _renderEngine.EvictFromCache(pageViewModel.FilePath, pageViewModel.PageNumber);
    }

    /// <summary>
    /// Compiles the layout data modifications back into a crisp, vector-based native PDF file.
    /// This preserves selectable text and keeps file sizes incredibly small.
    /// </summary>
    public async Task SaveDocumentAsync(string outputFilePath, IList<PdfPageViewModel> pageModels)
    {
        // Execute on a background thread to prevent UI freezing during large file compilations
        await Task.Run(() =>
        {
            using (var builder = new PdfDocumentBuilder())
            {
                // Use a built-in standard font for vector text embedding
                var standardFont = builder.AddStandard14Font(Standard14Font.Helvetica);

                foreach (var pageModel in pageModels)
                {
                    // 1. Create a clean page canvas mirroring the original dimensions
                    var newPage = builder.AddPage(pageModel.Width, pageModel.Height);

                    // 2. Loop through all extracted words on this page
                    if (pageModel.WordsData != null)
                    {
                        foreach (var word in pageModel.WordsData)
                        {
                            // Invert coordinate spaces back to PDF standard format if necessary
                            // PDF coordinates usually place (0,0) at the bottom-left corner
                            double invertedY = pageModel.Height - word.Y;

                            // Draw the word explicitly back onto the vector document layer
                            newPage.AddText(
                                word.Text,
                                word.FontSize,
                                new UglyToad.PdfPig.Core.PdfPoint(word.X, invertedY),
                                standardFont
                            );
                        }
                    }
                }

                // 3. Write out the final binary data stream to disk
                using (FileStream fs = File.Create(outputFilePath))
                {
                    byte[] documentBytes = builder.Build();
                    fs.Write(documentBytes, 0, documentBytes.Length);
                }
            }
        });
    }

    /// <summary>
    /// Proxy forwarder method passing global typography indexing directly down into the optimized render engine layer context.
    /// </summary>
    public List<PdfGlobalStyle> ScanDocumentGlobalStyles(string filePath)
    {
        // Routes the file path straight down into your preheated engine implementation core safely
        return _renderEngine.ScanDocumentGlobalStyles(filePath);
    }
}
