using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Sensus.ViewModels;

namespace Sensus.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _isClosing;
        private bool _canClose;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => UpdateWindowFrame();
        }

        #region Window Lifecycle

        protected override async void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (e.Cancel || _canClose || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            e.Cancel = true;

            if (_isClosing)
            {
                return;
            }

            _isClosing = true;
            IsEnabled = false;

            try
            {
                await viewModel.Workspace.ShutdownAsync();

                _canClose = true;

                // Queue the second close even when shutdown completed synchronously.
                _ = Dispatcher.BeginInvoke(new Action(Close));
            }
            catch (Exception ex)
            {
                IsEnabled = true;
                _isClosing = false;

                MessageBox.Show(this, ex.Message, "Unable to close Sensus", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Window Chrome

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            UpdateWindowFrame();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            UpdateWindowFrame();
        }

        private void UpdateWindowFrame()
        {
            if (WindowLayout is null)
            {
                return;
            }

            // Remove the strong border thickness when the window is maximized.
            WindowBorder.BorderThickness = WindowState == WindowState.Maximized ? new(0) : new(1);

            if (WindowState != WindowState.Maximized)
            {
                WindowLayout.Margin = new(0);
                return;
            }

            // Compensate for the hidden resize frame when maximized.
            // https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Shell/WindowChromeWorker.cs#L695-L699
            // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getsystemmetricsfordpi

            const int SM_CXSIZEFRAME = 32;
            const int SM_CYSIZEFRAME = 33;
            const int SM_CXPADDEDBORDER = 92;

            var dpi = VisualTreeHelper.GetDpi(this);
            var nativeDpi = (uint)Math.Round(96 * dpi.DpiScaleX);
            var padding = GetSystemMetricsForDpi(SM_CXPADDEDBORDER, nativeDpi);

            var horizontal = (GetSystemMetricsForDpi(SM_CXSIZEFRAME, nativeDpi) + padding) / dpi.DpiScaleX;
            var vertical = (GetSystemMetricsForDpi(SM_CYSIZEFRAME, nativeDpi) + padding) / dpi.DpiScaleY;

            WindowLayout.Margin = new(horizontal, vertical, horizontal, vertical);
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

    }
}
