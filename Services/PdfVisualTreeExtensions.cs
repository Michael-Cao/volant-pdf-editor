using System.Windows;
using System.Windows.Media;
using SkiaSharp.Views.WPF;

namespace pdf_editor2.Services
{
    /// <summary>
    /// Static utility extension specialized in thread-safe visual tree analysis.
    /// Completely isolated from WPF layout evaluation events to protect native virtualization performance.
    /// </summary>
    public static class PdfVisualTreeExtensions
    {
        /// <summary>
        /// Recursively traverses the active structural framework tree to locate and forcefully redraw visible SKElement contexts.
        /// </summary>
        public static void FindAndInvalidateSkiaElements(DependencyObject parent)
        {
            if (parent == null) return;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is SKElement skElement)
                {
                    skElement.InvalidateVisual();
                }
                else
                {
                    FindAndInvalidateSkiaElements(child);
                }
            }
        }
    }
}
