using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace pdf_editor2.Views.SideViews
{
    /// <summary>
    /// Interaction logic for BomPanelView.xaml
    /// </summary>
    public partial class ObjectInspectorView : UserControl
    {
        // Bindable collection tracking active graphic elements on the view canvas layout
        public ObservableCollection<PdfPageElement> ElementsList { get; set; }

        public ObjectInspectorView()
        {
            InitializeComponent();

            ElementsList = new ObservableCollection<PdfPageElement>();
            GridPageElements.ItemsSource = ElementsList;
        }

        /// <summary>
        /// Public method to load and refresh the element list from the active PDF page stream
        /// </summary>
        public void UpdatePageElements(List<PdfPageElement> newElements)
        {
            ElementsList.Clear();
            if (newElements != null)
            {
                foreach (var element in newElements)
                {
                    ElementsList.Add(element);
                }
            }
            UpdateTotalElementsCount();
        }

        /// <summary>
        /// Recalculates and updates the total element count label metrics layer.
        /// </summary>
        private void UpdateTotalElementsCount()
        {
            //if (TxtTotalElementsCount != null)
            //{
            //    TxtTotalElementsCount.Text = $"{ElementsList.Count} Objects";
            //}
        }

        /// <summary>
        /// Handles row-level icon click interactions to toggle the context preview overlay panel.
        /// </summary>
        private void BtnInspectText_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // Traverse the logical structural tree framework to find the sibling Popup control element handle
                if (btn.Parent is Grid parentGrid)
                {
                    var popup = parentGrid.FindName("TextDetailPopup") as Popup;
                    if (popup != null)
                    {
                        // Toggle the popup visible state. StaysOpen=False ensures clicking outside automatically hides it.
                        popup.IsOpen = !popup.IsOpen;
                    }
                }
            }
        }

        private void GridPageElements_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridPageElements.SelectedItem != null)
            {
                // 🛠️ CRITICAL FIX: Direct context projection bypasses any broken MVVM binding chains.
                // This forces the properties grid template inside DetailPanel to read the highlighted row model instantly!
                if (DetailPanel != null)
                {
                    DetailPanel.DataContext = GridPageElements.SelectedItem;
                    DetailPanel.Visibility = System.Windows.Visibility.Visible;
                }

                if (PanelSplitter != null)
                {
                    PanelSplitter.Visibility = System.Windows.Visibility.Visible;
                }
            }
            else
            {
                // Clear context structures safely when selection state is empty
                if (DetailPanel != null)
                {
                    DetailPanel.DataContext = null;
                    DetailPanel.Visibility = System.Windows.Visibility.Collapsed;
                }

                if (PanelSplitter != null)
                {
                    PanelSplitter.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }
    }

    /// <summary>
    /// Data object blueprint representing an individual visual primitive item parsed from a PDF page stream.
    /// </summary>
    public class PdfPageElement
    {
        public int ElementId { get; set; }
        public string ElementType { get; set; } = string.Empty; // Text, Path, Image, Shading
        public string Details { get; set; } = string.Empty;     // Plaintext or shape metrics layout
        public string BoundingBox { get; set; } = string.Empty; // Coordinate bounds: X, Y, Width, Height
    }
}
