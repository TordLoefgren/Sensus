using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Sensus.Models;
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
            // Workspace state.
            services.AddSingleton<AcquisitionState>();

            // Application shell.
            services.AddSingleton<MainViewModel>();
            services.AddSingleton(serviceProvider => new MainWindow
            {
                DataContext = serviceProvider.GetRequiredService<MainViewModel>()
            });

            // Viewmodel factories.
            services.AddSingleton<IWorkspaceViewModelFactory, WorkspaceViewModelFactory>();
            services.AddSingleton<IScannerViewModelFactory, ScannerViewModelFactory>();
            services.AddSingleton<IScanViewModelFactory, ScanViewModelFactory>();
            services.AddSingleton<IInspectorViewModelFactory, InspectorViewModelFactory>();
            services.AddSingleton<IStatusBarViewModelFactory, StatusBarViewModelFactory>();

            // Services.
            services.AddSingleton<ISerialConnectionService, SerialConnectionService>();
            services.AddSingleton<IAcquisitionService, AcquisitionService>();
            services.AddSingleton<IScannerProtocolService, ScannerProtocolService>();
            services.AddSingleton<IScannerSimulationService, ScannerSimulationService>();
        }
    }
}
