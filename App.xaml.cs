using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using pdf_editor2.Services;
using pdf_editor2.ViewModels;
using pdf_editor2.Views;
using pdf_editor2.Views;
using System.Configuration;
using System.Data;
using System.Windows;

namespace pdf_editor2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Initialize our service collection
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            // 2. Build the IoC container
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // 3. Resolve the Main View, set its DataContext, and display it
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure =>
            {
                configure.AddDebug(); // Writes logs straight out to the Visual Studio Output Window
                configure.SetMinimumLevel(LogLevel.Debug); // Capture low-level execution paths
                configure.AddFile("logs/app.log", append: true);
            });

            // --- LOW-LEVEL RENDER ENGINE SUBSYSTEMS ---
            // Register your internal parsing tools. Assuming these types exist or are simple objects:
            services.AddSingleton<PdfPageResolver>(provider =>
                new PdfPageResolver(new Dictionary<pdf_editor2.Models.PdfIndirectRef, object>())
            );

            services.AddSingleton<PdfPageCache>();

            services.AddSingleton<PdfFontMetricsManager>();

            // Registered as a stateless Singleton because it transforms raw document models on demand
            services.AddSingleton<PdfLayoutEngine>();

            // Register the Core Processing Engine
            services.AddSingleton<PdfRenderEngine>();

            // --- HIGH-LEVEL COORDINATION SERVICES ---
            services.AddSingleton<PdfService>();

            // --- VIEWMODELS (State Containers) ---
            services.AddSingleton<DiagnosticsViewModel>();
            services.AddTransient<PdfWorkspaceViewModel>();
            services.AddTransient<MainViewModel>();

            services.AddTransient<PdfWorkspaceView>(provider =>
                new PdfWorkspaceView(
                    provider.GetRequiredService<PdfService>(),
                    provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PdfWorkspaceView>>()
                )
            );

            // --- VIEWS (Layout Containers) ---
            // We register MainWindow/MainView so it can automatically pull its MainViewModel dependency
            services.AddSingleton<MainWindow>(provider =>
            {
                return new MainWindow(
                    provider.GetRequiredService<MainViewModel>(),
                    provider.GetRequiredService<PdfWorkspaceView>()
                );
            });
        }
    }

}
