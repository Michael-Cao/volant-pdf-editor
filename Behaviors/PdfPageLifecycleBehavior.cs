using pdf_editor2.Models;
using pdf_editor2.Services;
using pdf_editor2.ViewModels;
using System.Windows;

namespace pdf_editor2.Behaviors
{
    public static class PdfPageLifecycleBehavior
    {
        public static readonly DependencyProperty ObserveLifecycleProperty =
            DependencyProperty.RegisterAttached("ObserveLifecycle", typeof(bool), typeof(PdfPageLifecycleBehavior),
                new PropertyMetadata(false, OnObserveLifecycleChanged));

        public static void SetObserveLifecycle(DependencyObject element, bool value) => element.SetValue(ObserveLifecycleProperty, value);
        public static bool GetObserveLifecycle(DependencyObject element) => (bool)element.GetValue(ObserveLifecycleProperty);

        private static void OnObserveLifecycleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                element.Loaded -= OnElementLoaded;
                element.Unloaded -= OnElementUnloaded;

                if ((bool)e.NewValue)
                {
                    element.Loaded += OnElementLoaded;
                    element.Unloaded += OnElementUnloaded;
                }
            }
        }

        private static async void OnElementLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is PdfPageViewModel page && !page.IsRendered)
            {
                // Resolve your service cleanly via App/DI container or DataContext locator
                var pdfService = App.ServiceProvider.GetService(typeof(PdfService)) as PdfService;
                if (pdfService != null) await pdfService.MaterializePageUiAsync(page);
            }
        }

        private static void OnElementUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is PdfPageViewModel page && page.IsRendered)
            {
                var pdfService = App.ServiceProvider.GetService(typeof(PdfService)) as PdfService;
                pdfService?.RequestPageUnload(page);
            }
        }
    }
}
