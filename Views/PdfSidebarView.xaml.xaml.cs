using pdf_editor2.Views.SideViews;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// Interaction logic for PdfSidebarView.xaml
    /// </summary>
    public partial class PdfSidebarView : UserControl
    {
        public PdfSidebarView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Forwards the extracted PDF elements directly to the underlying Object Inspector DataGrid
        /// </summary>
        public void RefreshCurrentPageObjects(List<PdfPageElement> elements)
        {
            if (ObjectInspectorPanel != null)
            {
                ObjectInspectorPanel.UpdatePageElements(elements);
            }
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            var clickedButton = sender as ToggleButton;
            if (clickedButton == null) return;

            // Uncheck all other buttons when one button gets selected
            if (BtnPages != null && clickedButton != BtnPages) BtnPages.IsChecked = false;
            if (BtnBookmarks != null && clickedButton != BtnBookmarks) BtnBookmarks.IsChecked = false;
            if (BtnAnnotations != null && clickedButton != BtnAnnotations) BtnAnnotations.IsChecked = false;
        }
    }
}
