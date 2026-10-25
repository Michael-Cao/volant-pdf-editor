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
            // 🛠️ CRITICAL INDICES FIX: Locate the immediate parent grid container securely
            if (GridPageElements.Parent is Grid workspaceGrid && workspaceGrid.RowDefinitions.Count >= 3)
            {
                var row0_DataGrid = workspaceGrid.RowDefinitions[0];    // Top row container slot
                var row2_DetailPanel = workspaceGrid.RowDefinitions[2]; // Bottom row container slot

                if (GridPageElements.SelectedItem != null)
                {
                    // 1. Sync data context safely to populate properties fields instantly
                    if (DetailPanel != null)
                    {
                        DetailPanel.DataContext = GridPageElements.SelectedItem;
                        DetailPanel.Visibility = Visibility.Visible;
                    }

                    if (PanelSplitter != null)
                    {
                        PanelSplitter.Visibility = Visibility.Visible;
                    }

                    // 🛠️ ACTIVE SELECTION MATRIX: Distribute space perfectly evenly (50/50 split star layout)
                    row0_DataGrid.Height = new GridLength(1, GridUnitType.Star);
                    row2_DetailPanel.Height = new GridLength(1, GridUnitType.Star);
                }
                else
                {
                    // 2. Safely wipe out local contextual boundaries when selection goes empty
                    if (DetailPanel != null)
                    {
                        DetailPanel.DataContext = null;
                        DetailPanel.Visibility = Visibility.Collapsed;
                    }

                    if (PanelSplitter != null)
                    {
                        PanelSplitter.Visibility = Visibility.Collapsed;
                    }

                    // 🛠️ EMPTY SELECTION MATRIX: Collapse the bottom slot completely to 0px (Auto with collapsed child),
                    // forcing Row 0 (DataGrid) to expand aggressively and absorb 100% full panel height!
                    row0_DataGrid.Height = new GridLength(1, GridUnitType.Star);
                    row2_DetailPanel.Height = GridLength.Auto;
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
