using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Sensus.Services;
using Sensus.ViewModels;
using Sensus.ViewModels.Factories;
using Sensus.Views;

namespace Sensus
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;

        public App()
        {
            // Build service provider.
            var services = new ServiceCollection();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();
        }

        private void Application_StartUp(object sender, StartupEventArgs e)
        {
            MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }

        private void Application_Exit(object sender, EventArgs e)
        {
            _serviceProvider.Dispose();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Application shell.
            services.AddSingleton<MainViewModel>();
            services.AddSingleton(serviceProvider => new MainWindow
            {
                DataContext = serviceProvider.GetRequiredService<MainViewModel>()
            });

            // Viewmodel factories.
            services.AddSingleton<IWorkspaceViewModelFactory, WorkspaceViewModelFactory>();
            services.AddSingleton<IScannerViewModelFactory, ScannerViewModelFactory>();
            services.AddSingleton<IViewportViewModelFactory, ViewportViewModelFactory>();
            services.AddSingleton<IInspectorViewModelFactory, InspectorViewModelFactory>();
            services.AddSingleton<IStatusBarViewModelFactory, StatusBarViewModelFactory>();

            // Services.
            services.AddSingleton<IRangeSampleSerializerService, RangeSampleSerializerService>();
            services.AddSingleton<IRangeObservationService, RangeObservationService>();
            services.AddSingleton<IScannerProtocolService, ScannerProtocolService>();
            services.AddSingleton<IRangeSampleReaderService, RangeSampleReaderService>();
            services.AddSingleton<IScannerSimulationService, ScannerSimulationService>();
        }
    }
}
