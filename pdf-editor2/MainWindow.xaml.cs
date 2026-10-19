using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using pdf_editor2.Models;
using pdf_editor2.Services;

namespace pdf_editor2
{
    public partial class MainWindow : Window
    {
        // Instantiate our new event-driven streaming background render framework engine
        private readonly PdfRenderEngine _renderEngine = new PdfRenderEngine();

        // This live reactive list handles automated dynamic layout updates
        private ObservableCollection<PdfPageViewModel> _activePages = new ObservableCollection<PdfPageViewModel>();

        public MainWindow()
        {
            InitializeComponent();

            // ⚡ SUBSCRIBE: Tell the main application to listen for pages coming out of the engine
            _renderEngine.OnPageParsed += RenderEngine_OnPageParsed;
        }

        private void OpenPdf_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "PDF Files (*.pdf)|*.pdf" };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                this.Title = $"PDF Editor - {selectedFilePath}";
                TxtFilePath.Text = $"Active File:\n{selectedFilePath}";

                // Clear workspace cache layout fields for incoming tracking metrics
                _activePages.Clear();
                PagesControl.ItemsSource = _activePages;

                // 🚀 EMIT ROUTE: Tell the background engine to start processing and emitting events
                _renderEngine.StartStreamingDocument(selectedFilePath);
            }
        }

        // ⚡ LISTENER: Executes dynamically every time the engine finishes compiling a single page
        private void RenderEngine_OnPageParsed(object sender, PageParsedEventArgs e)
        {
            // Marshall execution securely back to the main UI loop thread context
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Drop the packaged page view directly into the UI Collection array list
                _activePages.Add(e.PageViewModel);
            });
        }

        private void ClosePdf_Click(object sender, RoutedEventArgs e)
        {
            _activePages.Clear();
            PagesControl.ItemsSource = null;
            this.Title = "Nova PDF Editor";
            TxtFilePath.Text = "No file loaded";

            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void About_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Nova PDF Editor.");
    }
}
