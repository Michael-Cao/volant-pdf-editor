using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace pdf_editor2.Views.SideViews
{
    public partial class ObjectInspectorTemplates : ResourceDictionary
    {
        public ObjectInspectorTemplates()
        {
            InitializeComponent();
        }

        private void BtnInspectText_Click(object sender, RoutedEventArgs e)
        {
            // Locates the parent popup inside the template tree frame and flags it open
            if (sender is Button btn && btn.Parent is Grid parentGrid)
            {
                var popup = parentGrid.FindName("TextDetailPopup") as Popup;
                if (popup != null)
                {
                    popup.IsOpen = true;
                }
            }
        }
    }
}
