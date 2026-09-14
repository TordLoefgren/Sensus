using System.Windows;

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
            UpdateViewportScene();
        }

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
