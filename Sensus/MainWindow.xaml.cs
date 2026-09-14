using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Sensus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            InitializeViewportScene();

            Loaded += MainWindow_Loaded;
            ViewportCanvas.SizeChanged += ViewportCanvas_SizeChanged;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateWindowFrame();
            UpdateViewportScene();
        }

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

            if (WindowState != WindowState.Maximized)
            {
                WindowLayout.Margin = new Thickness(0);
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
            WindowLayout.Margin = new Thickness(horizontal, vertical, horizontal, vertical);
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        private void ViewportCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateViewportScene();
        }

        private void InitializeViewportScene()
        {
        }

        private void UpdateViewportScene()
        {
        }

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

    }
}
