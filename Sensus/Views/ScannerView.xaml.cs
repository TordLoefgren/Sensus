using System.Windows;
using System.Windows.Controls;
using Sensus.ViewModels;

namespace Sensus.Views
{
    public partial class ScannerView : UserControl
    {
        public ScannerView()
        {
            InitializeComponent();
        }

        private async void RefreshSerialPortsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ScannerViewModel viewModel)
            {
                await viewModel.RefreshSerialPortsAsync();
            }
        }

        private async void SerialConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ScannerViewModel viewModel)
            {
                await viewModel.ToggleSerialConnectionAsync();
            }
        }

        private async void SimulationButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ScannerViewModel viewModel)
            {
                await viewModel.ToggleSimulationAsync();
            }
        }

        private void ClearSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ScannerViewModel viewModel)
            {
                viewModel.ClearSession();
            }
        }
    }
}
