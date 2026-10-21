using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace pdf_editor2.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly PdfService _pdfService;
        private string _statusMessage = "Ready";

        // Expose the child workspace viewmodel so MainView can bind its layout container directly
        public PdfWorkspaceViewModel Workspace { get; }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private readonly DiagnosticsViewModel _diagnostics;
        public DiagnosticsViewModel Diagnostics => _diagnostics;

        public MainViewModel(PdfWorkspaceViewModel workspaceViewModel, DiagnosticsViewModel diagnostics, PdfService pdfService)
        {
            Workspace = workspaceViewModel;
            _diagnostics = diagnostics;
            _pdfService = pdfService;
        }

        /// <summary>
        /// Routed target hit handler executed when a user selects 'Open File' via MenuBar commands.
        /// </summary>
        public void HandleOpenFileExecution()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PDF Documents (*.pdf)|*.pdf",
                Title = "Select a PDF File to Edit"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Route the file string straight down to the workspace component safely
                Workspace.LoadNewDocument(openFileDialog.FileName);
            }
        }

        /// <summary>
        /// Prompts the user for a destination file path and triggers the background vector PDF save operation.
        /// </summary>
        public async Task HandleSaveFileExecutionAsync()
        {
            if (Workspace.ActivePages.Count == 0)
            {
                MessageBox.Show("No active document loaded to save.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                Title = "Save Searchable Vector PDF"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    StatusMessage = "Compiling vector data structures into native PDF blocks...";

                    // ⚡ Delegate the heavy vector compilation out to your service loop
                    await _pdfService.SaveDocumentAsync(saveFileDialog.FileName, Workspace.ActivePages);

                    StatusMessage = "Document saved successfully as a searchable, vector PDF.";
                    MessageBox.Show("File exported clean without image flattening!", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = "Save pipeline aborted due to file locks or engine syntax errors.";
                    MessageBox.Show($"Export error encountered: {ex.Message}", "Export Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Cleans up active memory trees when a document is closed.
        /// </summary>
        public void HandleCloseFileExecution()
        {
            // Evict presentation buffers out of RAM
            foreach (var page in Workspace.ActivePages)
            {
                _pdfService.RequestPageUnload(page);
            }

            Workspace.ActivePages.Clear();
            Workspace.TotalDocumentPages = 0;
            Workspace.CurrentPageNumber = 1;

            StatusMessage = "Ready";

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
