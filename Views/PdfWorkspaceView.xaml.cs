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
    /// Interaction logic for PdfWorkspaceView.xaml
    /// </summary>
    public partial class PdfWorkspaceView : UserControl
    {
        public PdfWorkspaceView()
        {
            InitializeComponent();
        }

        private async void WorkspaceScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
        }
    }
}
