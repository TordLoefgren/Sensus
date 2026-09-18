using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Sensus.Extensions;
using Sensus.Services;

namespace Sensus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly record struct ViewportRenderParams(
            double Left,
            double Top,
            double Width,
            double Height,
            double OriginScreenX,
            double OriginScreenY,
            double CmToPixels
        );

        private readonly Brush _viewportGridLineBrush;
        private readonly Brush _viewportRulerBackgroundBrush;
        private readonly Brush _viewportRulerLineBrush;
        private readonly Brush _viewportRulerTextBrush;
        private readonly Brush _viewportMeasurementBrush;
        private readonly Brush _viewportMeasurementErrorBrush;
        private readonly Brush _viewportRangeStrokeBrush;
        private readonly Brush _viewportRangeFillBrush;

        // Transport.
        private const string SerialPortNameDefault = "DEFAULT";
        private const int BaudRateDefault = 9600;

        private readonly SerialPort _serialPort = new() { NewLine = "\r\n" };
        private ScannerSimulationStream? _scannerSimulationStream;

        // Protocol.
        private readonly RangeSampleSerializerService _rangeSampleSerializerService;

        // Processing.
        private CancellationTokenSource? inputProcessingCancellationTokenSource;
        private Task? inputProcessingTask;
        private Stream? inputProcessingStream;

        // Presentation / Rendering.
        private const double MaxRangeCm = 400;
        private const double MinRangeCm = 2;
        private const double ViewportHeightCm = 1200;
        private const int GridLineSpacingCm = 100;
        private const int RulerThicknessPixels = 30;
        private const int StrokeThicknessSmall = 1;
        private const int StrokeThicknessMedium = 2;

        public ObservableCollection<string> AvailablePortNames { get; private set; } = [];

        private readonly List<Line> _gridLines = [];
        private readonly List<TextBlock> _gridRulerLabels = [];
        private readonly List<Line> _gridRulerTicks = [];

        private string selectedSerialPortName = SerialPortNameDefault;
        private double latestDistanceCm = 0;
        private Point? latestMousePositionCm;

        private Line measurementLine = new();
        private Ellipse measurementEllipse = new();
        private Ellipse maxRangeEllipse = new();
        private Ellipse minRangeEllipse = new();

        private Rectangle rulerBackgroundRectangleHorizontal = new();
        private Rectangle rulerBackgroundRectangleVertical = new();
        private Rectangle rulerBackgroundRectangleCorner = new();
        private Line rulerBorderLineHorizontal = new();
        private Line rulerBorderLineVertical = new();
        private Line rulerBorderLineDiagonal = new();

        private TextBlock mouseHoverPositionLabel = new();
        private Rectangle mouseHoverPositionLabelBackground = new();
        private Rectangle canvasBorderRectangle = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _viewportGridLineBrush = (Brush)FindResource("ViewportGridLineBrush");
            _viewportRulerBackgroundBrush = (Brush)FindResource("ViewportRulerBackgroundBrush");
            _viewportRulerLineBrush = (Brush)FindResource("ViewportRulerLineBrush");
            _viewportRulerTextBrush = (Brush)FindResource("ViewportRulerTextBrush");
            _viewportMeasurementBrush = (Brush)FindResource("ViewportMeasurementBrush");
            _viewportMeasurementErrorBrush = (Brush)FindResource("ViewportMeasurementErrorBrush");
            _viewportRangeStrokeBrush = (Brush)FindResource("ViewportRangeStrokeBrush");
            _viewportRangeFillBrush = (Brush)FindResource("ViewportRangeFillBrush");

            _rangeSampleSerializerService = new();

            InitializeViewport();
            RefreshSerialPorts();

            Loaded += MainWindow_Loaded;

            ViewportCanvas.SizeChanged += ViewportCanvas_SizeChanged;
            ViewportCanvas.MouseMove += ViewportCanvas_MouseMove;
            ViewportCanvas.MouseLeave += ViewportCanvas_MouseLeave;
        }

        #region Processing

        private async Task ProcessInput(Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                using StreamReader reader = new(stream, leaveOpen: true);

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
                {
                    // Protocol.
                    if (!_rangeSampleSerializerService.TryDeserialize(line, out var rangeSample))
                    {
                        continue;
                    }

                    // Domain.
                    var roundTripDurationUs = rangeSample.RoundTripDurationUs;
                    var distanceCm = CalculateDistanceCm(roundTripDurationUs);

                    // Presentation / Rendering.
                    await Dispatcher.InvokeAsync(() =>
                    {
                        latestDistanceCm = distanceCm;

                        RenderViewport(distanceCm);
                        UpdateSample(roundTripDurationUs, distanceCm);
                    });
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is the expected completion path when changing inputs or closing.
            }
            catch (IOException ex) when (!cancellationToken.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateConnectionStatus("Error");
                    UpdateError(ex.Message);
                });
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                // Closing a stream is part of the expected cancellation path.
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateConnectionStatus("Error");
                    UpdateError(ex.Message);
                });
            }
        }

        private async Task StartInputProcessing(Stream stream)
        {
            await StopInputProcessing();

            inputProcessingCancellationTokenSource = new();
            inputProcessingStream = stream;
            inputProcessingTask = ProcessInput(stream, inputProcessingCancellationTokenSource.Token);
        }

        private async Task StopInputProcessing()
        {
            // Snapshot the previous session to make sure cleanup cannot target the replacement.
            var task = inputProcessingTask;
            var cancellationTokenSource = inputProcessingCancellationTokenSource;
            var stream = inputProcessingStream;

            inputProcessingTask = null;
            inputProcessingCancellationTokenSource = null;
            inputProcessingStream = null;

            if (task is null)
            {
                return;
            }

            cancellationTokenSource?.Cancel();

            // Close serial before awaiting and dispose simulation after its read ends.
            var disposeBeforeAwait = stream is not ScannerSimulationStream;
            if (disposeBeforeAwait && stream is not null)
            {
                try
                {
                    await stream.DisposeAsync();
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            try
            {
                await task;
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException)
            {
                // Cancellation and stream shutdown are expected during cleanup.
            }
            finally
            {
                if (!disposeBeforeAwait && stream is not null)
                {
                    await stream.DisposeAsync();
                }

                cancellationTokenSource?.Dispose();
            }
        }

        #endregion


        #region Transport

        private void RefreshSerialPorts()
        {
            var serialPortNames = SerialPort.GetPortNames();

            AvailablePortNames.Clear();
            AvailablePortNames.AddRange(serialPortNames);

            if (selectedSerialPortName != SerialPortNameDefault && !serialPortNames.Contains(selectedSerialPortName))
            {
                CloseSerialPort();
                selectedSerialPortName = SerialPortNameDefault;

                return;
            }

            if (!_serialPort.IsOpen)
            {
                Dispatcher.InvokeAsync(() => UpdateConnectionStatus("Disconnected"));

                return;
            }

            Dispatcher.InvokeAsync(() => UpdateConnectionStatus("Connected"));

            ValidPortsComboBox.SelectedIndex = serialPortNames.IndexOf(selectedSerialPortName);
        }

        private bool OpenSerialPort(string portName, int baudRate)
        {
            _serialPort.PortName = portName;
            _serialPort.BaudRate = baudRate;

            try
            {
                _serialPort.Open();
                Dispatcher.InvokeAsync(() => UpdateConnectionStatus("Connected"));
                return true;
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    UpdateConnectionStatus("Error");
                    UpdateError(ex.Message);
                });
                return false;
            }
        }

        private void CloseSerialPort()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            _serialPort.PortName = SerialPortNameDefault;
            _serialPort.BaudRate = BaudRateDefault;

            Dispatcher.InvokeAsync(() => UpdateConnectionStatus("Disconnected"));
        }

        #endregion


        #region Domain

        private double CalculateDistanceCm(uint roundTripDurationUs)
        {
            // Convert round trip pulse duration in microseconds to distance in cm.
            return roundTripDurationUs * 0.034 / 2;
        }

        #endregion


        #region Presentation

        private void UpdateConnectionStatus(string status)
        {
            SerialPortConnectionStatusTextBlock.Text = $"Connection status: {status}";
        }

        private void UpdateSample(uint? roundTripDurationUs, double? distanceCm)
        {
            RoundTripDurationUsTextBlock.Text = roundTripDurationUs.HasValue
                ? $"Round trip duration: {roundTripDurationUs} μs"
                : $"Round trip duration: -";
            DistanceCmTextBlock.Text = roundTripDurationUs.HasValue
                ? $"Distance: {distanceCm:F2} cm"
                : $"Distance: -";
        }

        private void UpdateError(string? message)
        {
            ErrorMessageTextBlock.Text = $"Error message: {message}";

            ErrorMessageTextBlock.Visibility = string.IsNullOrEmpty(message)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        #endregion


        #region Rendering

        private double WorldToScreenX(double worldX, double originScreenX, double scale)
        {
            return originScreenX + worldX * scale;
        }

        private double WorldToScreenY(double worldY, double originScreenY, double scale)
        {
            return originScreenY - worldY * scale;
        }

        private double ScreenToWorldX(double screenX, double originScreenX, double scale)
        {
            return (screenX - originScreenX) / scale;
        }

        private double ScreenToWorldY(double screenY, double originScreenY, double scale)
        {
            return (originScreenY - screenY) / scale;
        }

        private ViewportRenderParams CalculateViewportRenderParams(
            double canvasWidth,
            double canvasHeight
        )
        {
            var left = (double)RulerThicknessPixels;
            var top = (double)RulerThicknessPixels;

            var width = canvasWidth - left;
            var height = canvasHeight - top;

            var originScreenX = left + width / 2;
            var originScreenY = top + height / 2;

            var cmToPixels = height / ViewportHeightCm;

            return new(left, top, width, height, originScreenX, originScreenY, cmToPixels);
        }

        private void InitializeCanvasElements()
        {
            canvasBorderRectangle = new()
            {
                Stroke = _viewportRulerLineBrush,
                StrokeThickness = StrokeThicknessSmall
            };

            ViewportCanvas.Children.Add(canvasBorderRectangle);
        }

        private void InitializeMeasurements()
        {
            measurementLine.Stroke = _viewportMeasurementBrush;
            measurementLine.StrokeThickness = StrokeThicknessMedium;

            measurementEllipse.Stroke = null;
            measurementEllipse.Fill = _viewportMeasurementBrush;

            ViewportCanvas.Children.Add(measurementLine);
            ViewportCanvas.Children.Add(measurementEllipse);
        }

        private void InitializeRanges()
        {
            maxRangeEllipse.Stroke = _viewportRangeStrokeBrush;
            maxRangeEllipse.StrokeThickness = StrokeThicknessMedium;
            maxRangeEllipse.StrokeDashArray = [1, 2];
            maxRangeEllipse.Fill = _viewportRangeFillBrush;

            minRangeEllipse.Stroke = _viewportRangeStrokeBrush;
            minRangeEllipse.StrokeThickness = StrokeThicknessMedium;
            minRangeEllipse.StrokeDashArray = [1, 2];
            minRangeEllipse.Fill = _viewportRangeFillBrush;

            ViewportCanvas.Children.Add(maxRangeEllipse);
            ViewportCanvas.Children.Add(minRangeEllipse);
        }

        private void InitializeMousePosition()
        {
            mouseHoverPositionLabel.Foreground = _viewportRulerTextBrush;
            mouseHoverPositionLabel.Text = "";
            mouseHoverPositionLabel.HorizontalAlignment = HorizontalAlignment.Right;

            mouseHoverPositionLabelBackground = new()
            {
                Stroke = _viewportRulerLineBrush,
                StrokeThickness = StrokeThicknessSmall,
                Fill = _viewportRulerBackgroundBrush
            };

            ViewportCanvas.Children.Add(mouseHoverPositionLabel);
            ViewportCanvas.Children.Add(mouseHoverPositionLabelBackground);
        }

        private void InitializeViewport()
        {
            InitializeCanvasElements();
            InitializeMeasurements();
            InitializeRanges();
            InitializeMousePosition();
        }

        private (double Width, double Height) GetTextBlockSize(TextBlock textBlock)
        {
            // https://learn.microsoft.com/en-us/answers/questions/400490/how-to-get-the-actual-width-of-textblock
            var formatted = new FormattedText(
                textBlock.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize, textBlock.Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip
            );

            return (formatted.Width, formatted.Height);
        }

        private (int Count, int HorizontalCount, int VerticalCount) CalculateRequiredLineCount(
            ViewportRenderParams viewportRenderParams
        )
        {
            var viewportWidthCm = viewportRenderParams.Width / viewportRenderParams.CmToPixels;

            var verticalLineCount = (int)Math.Ceiling(viewportWidthCm / GridLineSpacingCm) + 1;
            var horizontalLineCount = (int)Math.Ceiling(ViewportHeightCm / GridLineSpacingCm) + 1;

            return (
                horizontalLineCount + verticalLineCount,
                horizontalLineCount,
                verticalLineCount
            );
        }

        private void EnsureGridElements(int requiredCount)
        {
            foreach (var line in _gridLines)
            {
                ViewportCanvas.Children.Remove(line);
            }

            _gridLines.Clear();

            for (int i = 0; i < requiredCount; i++)
            {
                var line = new Line
                {
                    Stroke = _viewportGridLineBrush,
                    StrokeThickness = StrokeThicknessSmall
                };

                _gridLines.Add(line);
            }

            foreach (var line in _gridLines)
            {
                ViewportCanvas.Children.Add(line);
            }
        }

        private void EnsureRulerElements(int requiredCount)
        {
            // Ruler background.
            ViewportCanvas.Children.Remove(rulerBackgroundRectangleHorizontal);
            ViewportCanvas.Children.Remove(rulerBackgroundRectangleVertical);
            ViewportCanvas.Children.Remove(rulerBackgroundRectangleCorner);

            rulerBackgroundRectangleHorizontal = new()
            {
                Fill = _viewportRulerBackgroundBrush
            };
            rulerBackgroundRectangleVertical = new()
            {
                Fill = _viewportRulerBackgroundBrush
            };
            rulerBackgroundRectangleCorner = new()
            {
                Fill = _viewportRulerBackgroundBrush
            };

            ViewportCanvas.Children.Add(rulerBackgroundRectangleHorizontal);
            ViewportCanvas.Children.Add(rulerBackgroundRectangleVertical);
            ViewportCanvas.Children.Add(rulerBackgroundRectangleCorner);

            // Ruler Borders.
            ViewportCanvas.Children.Remove(rulerBorderLineHorizontal);
            ViewportCanvas.Children.Remove(rulerBorderLineVertical);
            ViewportCanvas.Children.Remove(rulerBorderLineDiagonal);

            rulerBorderLineHorizontal = new()
            {
                Stroke = _viewportRulerLineBrush,
                StrokeThickness = StrokeThicknessSmall
            };
            rulerBorderLineVertical = new()
            {
                Stroke = _viewportRulerLineBrush,
                StrokeThickness = StrokeThicknessSmall
            };
            rulerBorderLineDiagonal = new()
            {
                Stroke = _viewportRulerLineBrush,
                StrokeThickness = StrokeThicknessSmall
            };

            ViewportCanvas.Children.Add(rulerBorderLineHorizontal);
            ViewportCanvas.Children.Add(rulerBorderLineVertical);
            ViewportCanvas.Children.Add(rulerBorderLineDiagonal);

            // Ruler labels.
            foreach (var textBlock in _gridRulerLabels)
            {
                ViewportCanvas.Children.Remove(textBlock);
            }

            _gridRulerLabels.Clear();

            foreach (var line in _gridRulerTicks)
            {
                ViewportCanvas.Children.Remove(line);
            }

            _gridRulerTicks.Clear();

            for (int i = 0; i < requiredCount; i++)
            {
                _gridRulerLabels.Add(new()
                {
                    Foreground = _viewportRulerTextBrush
                });
            }

            foreach (var label in _gridRulerLabels)
            {
                ViewportCanvas.Children.Add(label);
            }

            // Ruler ticks.
            for (int i = 0; i < requiredCount; i++)
            {
                _gridRulerTicks.Add(new()
                {
                    Stroke = _viewportRulerLineBrush,
                    StrokeThickness = StrokeThicknessSmall
                });
            }

            foreach (var tick in _gridRulerTicks)
            {
                ViewportCanvas.Children.Add(tick);
            }
        }

        private void RenderCanvasBorder(ViewportRenderParams viewportRenderParams)
        {
            canvasBorderRectangle.Width = viewportRenderParams.Width + viewportRenderParams.Left;
            canvasBorderRectangle.Height = viewportRenderParams.Height + viewportRenderParams.Top;

            Canvas.SetLeft(canvasBorderRectangle, 0);
            Canvas.SetTop(canvasBorderRectangle, 0);
        }

        private void RenderGrid(
            ViewportRenderParams viewportRenderParams,
            int horizontalCount,
            int verticalCount
        )
        {
            var gridSpacingPixels = GridLineSpacingCm * viewportRenderParams.CmToPixels;
            var offsetX = viewportRenderParams.OriginScreenX % gridSpacingPixels;
            var offsetY = viewportRenderParams.OriginScreenY % gridSpacingPixels;

            for (int i = 0; i < verticalCount; i++)
            {
                var x = offsetX + i * gridSpacingPixels;

                // TODO: Calculate the first visible grid line instead of skipping.
                if (x < viewportRenderParams.Left)
                {
                    continue;
                }

                var line = _gridLines[i];
                line.X1 = x;
                line.Y1 = viewportRenderParams.Top;
                line.X2 = x;
                line.Y2 = viewportRenderParams.Height + viewportRenderParams.Top;
            }

            for (int j = 0; j < horizontalCount; j++)
            {
                var y = offsetY + j * gridSpacingPixels;

                // TODO: Calculate the first visible grid line instead of skipping.
                if (y < viewportRenderParams.Top)
                {
                    continue;
                }

                var line = _gridLines[j + verticalCount];
                line.X1 = viewportRenderParams.Left;
                line.Y1 = y;
                line.X2 = viewportRenderParams.Width + viewportRenderParams.Left;
                line.Y2 = y;
            }
        }

        private void RenderRanges(ViewportRenderParams viewportRenderParams)
        {
            var maxRadius = MaxRangeCm * viewportRenderParams.CmToPixels;
            var minRadius = MinRangeCm * viewportRenderParams.CmToPixels;

            maxRangeEllipse.Width = maxRadius * 2;
            maxRangeEllipse.Height = maxRadius * 2;

            Canvas.SetLeft(maxRangeEllipse, viewportRenderParams.OriginScreenX - maxRadius);
            Canvas.SetTop(maxRangeEllipse, viewportRenderParams.OriginScreenY - maxRadius);

            minRangeEllipse.Width = minRadius * 2;
            minRangeEllipse.Height = minRadius * 2;

            Canvas.SetLeft(minRangeEllipse, viewportRenderParams.OriginScreenX - minRadius);
            Canvas.SetTop(minRangeEllipse, viewportRenderParams.OriginScreenY - minRadius);
        }

        private void RenderMeasurement(ViewportRenderParams viewportRenderParams, double distanceCm)
        {
            var stroke = distanceCm > MaxRangeCm
                ? _viewportMeasurementErrorBrush
                : _viewportMeasurementBrush;

            var measurementHeight = distanceCm * viewportRenderParams.CmToPixels;

            measurementLine.X1 = viewportRenderParams.OriginScreenX;
            measurementLine.Y1 = viewportRenderParams.OriginScreenY;
            measurementLine.X2 = viewportRenderParams.OriginScreenX;
            measurementLine.Y2 = viewportRenderParams.OriginScreenY - measurementHeight;

            measurementLine.Stroke = stroke;

            var radius = 4;

            measurementEllipse.Width = radius * 2;
            measurementEllipse.Height = radius * 2;
            measurementEllipse.Fill = stroke;

            Canvas.SetLeft(
                measurementEllipse,
                measurementLine.X2 - radius
            );
            Canvas.SetTop(
                measurementEllipse,
                measurementLine.Y2 - radius
            );
        }

        private void RenderRulers(
            ViewportRenderParams viewportRenderParams,
            int horizontalCount,
            int verticalCount
        )
        {
            // Ruler background.
            rulerBackgroundRectangleHorizontal.Width = viewportRenderParams.Width;
            rulerBackgroundRectangleHorizontal.Height = viewportRenderParams.Top;

            Canvas.SetLeft(rulerBackgroundRectangleHorizontal, viewportRenderParams.Left);
            Canvas.SetTop(rulerBackgroundRectangleHorizontal, 0);

            rulerBackgroundRectangleVertical.Width = viewportRenderParams.Left;
            rulerBackgroundRectangleVertical.Height = viewportRenderParams.Height;

            Canvas.SetLeft(rulerBackgroundRectangleVertical, 0);
            Canvas.SetTop(rulerBackgroundRectangleVertical, viewportRenderParams.Top);

            rulerBackgroundRectangleCorner.Width = viewportRenderParams.Left;
            rulerBackgroundRectangleCorner.Height = viewportRenderParams.Top;

            Canvas.SetLeft(rulerBackgroundRectangleCorner, 0);
            Canvas.SetTop(rulerBackgroundRectangleCorner, 0);

            // Ruler borders.
            rulerBorderLineHorizontal.X1 = 0;
            rulerBorderLineHorizontal.Y1 = viewportRenderParams.Top;
            rulerBorderLineHorizontal.X2 = viewportRenderParams.Width + viewportRenderParams.Left;
            rulerBorderLineHorizontal.Y2 = viewportRenderParams.Top;

            rulerBorderLineVertical.X1 = viewportRenderParams.Left;
            rulerBorderLineVertical.Y1 = 0;
            rulerBorderLineVertical.X2 = viewportRenderParams.Left;
            rulerBorderLineVertical.Y2 = viewportRenderParams.Height + viewportRenderParams.Top;

            rulerBorderLineDiagonal.X1 = 0;
            rulerBorderLineDiagonal.Y1 = 0;
            rulerBorderLineDiagonal.X2 = viewportRenderParams.Left;
            rulerBorderLineDiagonal.Y2 = viewportRenderParams.Top;

            // Ruler labels and ruler ticks.
            var viewportWidthCm = viewportRenderParams.Width / viewportRenderParams.CmToPixels;

            var gridSpacingPixels = GridLineSpacingCm * viewportRenderParams.CmToPixels;
            var rulerLabelOffsetPixels = (RulerThicknessPixels / 2) - 5;
            var offsetX = viewportRenderParams.OriginScreenX % gridSpacingPixels;
            var offsetY = viewportRenderParams.OriginScreenY % gridSpacingPixels;

            var originLineIndexX = (int)((viewportRenderParams.OriginScreenX - offsetX) / gridSpacingPixels);
            var originLineIndexY = (int)((viewportRenderParams.OriginScreenY - offsetY) / gridSpacingPixels);

            for (int i = 0; i < verticalCount; i++)
            {
                var x = offsetX + i * gridSpacingPixels;

                var label = _gridRulerLabels[i];
                var labelValue = (i - originLineIndexX) * GridLineSpacingCm;

                label.Text = $"{labelValue} cm";

                var (labelWidth, _) = GetTextBlockSize(label);
                var labelLeft = x - labelWidth / 2;
                var labelRight = x + labelWidth / 2;

                Canvas.SetLeft(label, labelLeft);
                Canvas.SetTop(label, rulerLabelOffsetPixels);

                var labelFitsHorizontally = labelLeft >= RulerThicknessPixels && labelRight <= viewportRenderParams.Width;
                label.Visibility = labelFitsHorizontally ? Visibility.Visible : Visibility.Collapsed;

                var tick = _gridRulerTicks[i];
                tick.X1 = x;
                tick.Y1 = 0;
                tick.X2 = x;
                tick.Y2 = rulerLabelOffsetPixels;

                tick.Visibility = labelFitsHorizontally ? Visibility.Visible : Visibility.Collapsed;
            }

            for (int j = 0; j < horizontalCount; j++)
            {
                var y = offsetY + j * gridSpacingPixels;

                var label = _gridRulerLabels[j + verticalCount];
                var labelValue = (originLineIndexY - j) * GridLineSpacingCm;

                label.Text = $"{labelValue} cm";

                var (labelWidth, _) = GetTextBlockSize(label);
                var labelTop = y - labelWidth / 2;
                var labelBottom = y + labelWidth / 2;

                Canvas.SetLeft(label, rulerLabelOffsetPixels);
                Canvas.SetTop(label, labelTop);

                var labelFitsVertically = labelTop >= RulerThicknessPixels && labelBottom <= viewportRenderParams.Height;
                label.Visibility = labelFitsVertically ? Visibility.Visible : Visibility.Collapsed;

                label.LayoutTransform = new RotateTransform(270);

                var tick = _gridRulerTicks[j + verticalCount];
                tick.X1 = 0;
                tick.Y1 = y;
                tick.X2 = rulerLabelOffsetPixels;
                tick.Y2 = y;

                tick.Visibility = labelFitsVertically ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RenderMousePosition(ViewportRenderParams viewportRenderParams)
        {
            if (latestMousePositionCm is not Point point)
            {
                mouseHoverPositionLabel.Text = "";
                mouseHoverPositionLabel.Visibility = Visibility.Collapsed;

                mouseHoverPositionLabelBackground.Visibility = Visibility.Collapsed;

                return;
            }

            var x = point.X;
            var y = point.Y;

            var screenX = WorldToScreenX(
                x,
                viewportRenderParams.OriginScreenX,
                viewportRenderParams.CmToPixels
            );
            var screenY = WorldToScreenY(
                y,
                viewportRenderParams.OriginScreenY,
                viewportRenderParams.CmToPixels
            );

            mouseHoverPositionLabel.Text = $"{(int)x} × {(int)y}";
            mouseHoverPositionLabel.Visibility = Visibility.Visible;

            var (labelWidth, labelHeight) = GetTextBlockSize(mouseHoverPositionLabel);
            var padding = 25;

            var labelX = viewportRenderParams.Width - labelWidth - padding;
            var labelY = viewportRenderParams.Height - labelHeight - padding;

            Canvas.SetLeft(mouseHoverPositionLabel, labelX);
            Canvas.SetTop(mouseHoverPositionLabel, labelY);

            mouseHoverPositionLabelBackground.Width = labelWidth + padding;
            mouseHoverPositionLabelBackground.Height = labelHeight + padding;
            mouseHoverPositionLabelBackground.Visibility = Visibility.Visible;

            Canvas.SetLeft(mouseHoverPositionLabelBackground, labelX - padding / 2);
            Canvas.SetTop(mouseHoverPositionLabelBackground, labelY - padding / 2);
        }

        private void RenderViewport(double distanceCm)
        {
            if (ViewportCanvas.ActualWidth <= 0 || ViewportCanvas.ActualHeight <= 0)
            {
                return;
            }

            var viewportRenderParams = CalculateViewportRenderParams(
                ViewportCanvas.ActualWidth,
                ViewportCanvas.ActualHeight
            );

            var (requiredCount, horizontalCount, verticalCount) = CalculateRequiredLineCount(
                viewportRenderParams
            );

            EnsureGridElements(requiredCount);
            EnsureRulerElements(requiredCount);

            RenderCanvasBorder(viewportRenderParams);
            RenderGrid(viewportRenderParams, horizontalCount, verticalCount);
            RenderRanges(viewportRenderParams);
            RenderMeasurement(viewportRenderParams, distanceCm);
            RenderRulers(viewportRenderParams, horizontalCount, verticalCount);
            RenderMousePosition(viewportRenderParams);
        }

        #endregion


        #region Window / UI

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateWindowFrame();
            RenderViewport(latestDistanceCm);
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

        protected override void OnClosed(EventArgs e)
        {
            inputProcessingCancellationTokenSource?.Cancel();
            CloseSerialPort();
            _ = StopInputProcessing();

            base.OnClosed(e);
        }

        private void ViewportCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderViewport(latestDistanceCm);
        }

        private void ViewportCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (ViewportCanvas.Width <= 0 || ViewportCanvas.Height <= 0)
            {
                return;
            }

            var position = e.GetPosition(ViewportCanvas);

            var viewportRenderParams = CalculateViewportRenderParams(
                ViewportCanvas.ActualWidth,
                ViewportCanvas.ActualHeight
            );

            var worldX = ScreenToWorldX(
                position.X,
                viewportRenderParams.OriginScreenX,
                viewportRenderParams.CmToPixels
            );
            var worldY = ScreenToWorldY(
                position.Y,
                viewportRenderParams.OriginScreenY,
                viewportRenderParams.CmToPixels
            );

            latestMousePositionCm = new(worldX, worldY);

            RenderViewport(latestDistanceCm);
        }

        private void ViewportCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            latestMousePositionCm = null;

            RenderViewport(latestDistanceCm);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshSerialPorts();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSerialPortName == SerialPortNameDefault)
            {
                UpdateConnectionStatus("Error");
                UpdateError("Please select a serial port");

                return;
            }

            await StopInputProcessing();
            CloseSerialPort();

            if (OpenSerialPort(selectedSerialPortName, BaudRateDefault))
            {
                await StartInputProcessing(_serialPort.BaseStream);
            }
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            await StopInputProcessing();
            CloseSerialPort();

            latestDistanceCm = 0;

            RenderViewport(latestDistanceCm);
            UpdateSample(null, null);
        }

        private async void StartSimulationButton_Click(object sender, RoutedEventArgs e)
        {
            await StopInputProcessing();
            CloseSerialPort();

            _scannerSimulationStream = new(
                ScannerSimulationGenerator.IncreasingRangeCyclic,
                _rangeSampleSerializerService
            );

            await StartInputProcessing(_scannerSimulationStream);
        }

        private async void StopSimulationButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(inputProcessingStream, _scannerSimulationStream))
            {
                await StopInputProcessing();
            }

            _scannerSimulationStream = null;

            latestDistanceCm = 0;

            RenderViewport(latestDistanceCm);
            UpdateSample(null, null);
        }

        private void ValidPortsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedValue is string selectedValue)
            {
                selectedSerialPortName = selectedValue;
            }
        }

        #endregion


        #region Window Chrome

        private void UpdateWindowFrame()
        {
            if (WindowLayout is null)
            {
                return;
            }

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
