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
using pdf_editor2.ViewModels;

namespace pdf_editor2.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => DataContext as MainViewModel;
        private readonly PdfWorkspaceView _pdfWorkspace;

        public PdfWorkspaceView PdfWorkspace => _pdfWorkspace;

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(MainViewModel viewModel, PdfWorkspaceView pdfWorkspace) : this()
        {
            DataContext = viewModel;

            _pdfWorkspace = pdfWorkspace;

            _pdfWorkspace.DataContext = viewModel.Workspace;

            WorkspaceHost.Content = _pdfWorkspace;
        }

        private void PdfStatusBar_ZoomChanged(double zoom)
        {
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
                        PdfStatusBar.ZoomValue += 0.1;
                        break;

                    case Key.OemMinus:
                    case Key.Subtract:
                        e.Handled = true;
                        PdfStatusBar.ZoomValue -= 0.1;
                        break;

                    case Key.D0:
                    case Key.NumPad0:
                        e.Handled = true;
                        PdfStatusBar.ZoomValue = 1.0;
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
                    PdfStatusBar.ZoomValue += 0.1;
                }
                else if (e.Delta < 0)
                {
                    PdfStatusBar.ZoomValue -= 0.1;
                }
            }
        }

        private async void OpenPdf_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.HandleOpenFileExecution();
        }

        private async void SavePdf_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            await ViewModel.HandleSaveFileExecutionAsync();
        }

        private void ClosePdf_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.HandleCloseFileExecution();
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void About_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Nova PDF Editor.");
    }
}
