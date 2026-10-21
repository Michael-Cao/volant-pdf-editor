using pdf_editor2.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace pdf_editor2.Views
{
    /// <summary>
    /// Interaction logic for PdfStatusBarView.xaml
    /// </summary>
    public partial class PdfStatusBarView : UserControl
    {

        private MainViewModel ViewModel => DataContext as MainViewModel;

        public PdfStatusBarView()
        {
            InitializeComponent();
        }

        public double ZoomValue
        {
            get => ZoomSlider?.Value ?? 1.0;
            set
            {
                if (ZoomSlider != null)
                {
                    ZoomSlider.Value = Math.Clamp(value, ZoomSlider.Minimum, ZoomSlider.Maximum);
                }
            }
        }

        public void SetStatusMessage(string message)
        {
            if (TxtStatusMessage != null) TxtStatusMessage.Text = message;
        }

        public void SetPageStatus(int currentPage, int totalPages)
        {
            if (TxtPageStatus != null) TxtPageStatus.Text = $"Page {currentPage} of {totalPages}";
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Ensure controls and nested workspace properties are initialized before invoking modifications
            if (ViewModel?.Workspace == null || TxtZoomStatus == null) return;

            // 1. Update the ViewModel property directly
            ViewModel.Workspace.ZoomFactor = e.NewValue;

            // 2. Refresh text status indicators
            TxtZoomStatus.Text = $"{Math.Round(e.NewValue * 100)}%";

            // 3. Raise the optional backward-compatibility event hook if MainWindow is listening
            ZoomChanged?.Invoke(e.NewValue);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ZoomValue -= 0.1;
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ZoomValue += 0.1;
        }

        public event Action<double> ZoomChanged;
    }
}
