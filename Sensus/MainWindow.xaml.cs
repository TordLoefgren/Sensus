using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Sensus.Enums;
using Sensus.Extensions;
using Sensus.Models;
using Sensus.Services;

namespace Sensus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Fields and properties

        #region Transport

        private const string DefaultSerialPortName = "DEFAULT";
        private const string PreferredSerialPortName = "COM6";
        private const int DefaultBaudRate = 9600;

        private readonly SerialPort _serialPort = new() { NewLine = "\r\n" };

        private string _selectedSerialPortName = DefaultSerialPortName;

        #endregion


        #region Protocol

        private readonly RangeSampleSerializerService _rangeSampleSerializerService;

        #endregion


        #region Processing

        private CancellationTokenSource? _inputProcessingCancellationTokenSource;
        private Task? _inputProcessingTask;
        private Stream? _inputProcessingStream;

        #endregion


        #region Domain

        private readonly ScannerDefinition _scannerDefinition = new(
            "Sensus Rover",
            "Mk. 1-A",
            new("ELEGOO UNO R3", "ATmega328"),
            new("HC-SR04", 2.0, 400.0, 15.0),
            new("SG90", 180.0)
        );

        private readonly ScannerConfiguration _scannerConfiguration = new(
            -90.0,
            90.0,
            5.0,
            60,
            30_000
        );

        private ScannerSession? _activeSession;
        public ScannerSession? ActiveSession
        {
            get => _activeSession;
            set
            {
                if (_activeSession == value)
                {
                    return;
                }

                _activeSession = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(CanClearSession));
            }
        }

        #endregion


        #region Presentation

        public event PropertyChangedEventHandler? PropertyChanged;  // TODO: Remove once viewmodels are introduced.

        #region Source

        public bool CanUseSerial => SourceState == SourceState.Idle || SourceType == SourceType.Serial;
        public bool CanUseSimulation => SourceState == SourceState.Idle || SourceType == SourceType.Simulation;
        public bool CanClearSession => ActiveSession is not null && SourceState == SourceState.Idle;

        public string SerialConnectionButtonDisplay => SourceType == SourceType.Serial
            ? SourceState switch
            {
                SourceState.Connecting => "Cancel",
                SourceState.Active => "Disconnect",
                _ => "Connect"
            }
            : "Connect";

        public string SimulationButtonDisplay => SourceType == SourceType.Simulation
            ? SourceState switch
            {
                SourceState.Connecting => "Cancel",
                SourceState.Active => "Stop",
                _ => "Start"
            }
            : "Start";

        private SourceState _sourceState = SourceState.Idle;
        public SourceState SourceState
        {
            get => _sourceState;
            private set
            {
                if (_sourceState == value)
                {
                    return;
                }

                _sourceState = value;

                OnPropertyChanged(nameof(CanUseSerial));
                OnPropertyChanged(nameof(CanUseSimulation));
                OnPropertyChanged(nameof(CanClearSession));
                OnPropertyChanged(nameof(SourceStatusDisplay));
                OnPropertyChanged(nameof(SerialConnectionButtonDisplay));
                OnPropertyChanged(nameof(SimulationButtonDisplay));
            }
        }

        private SourceType _sourceType;
        public SourceType SourceType
        {
            get => _sourceType;
            private set
            {
                if (_sourceType == value)
                {
                    return;
                }

                _sourceType = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(CanUseSerial));
                OnPropertyChanged(nameof(CanUseSimulation));
                OnPropertyChanged(nameof(SerialConnectionButtonDisplay));
                OnPropertyChanged(nameof(SimulationButtonDisplay));
            }
        }

        private string? _sourceIdentity;
        public string? SourceIdentity
        {
            get => _sourceIdentity;
            private set
            {
                if (_sourceIdentity == value)
                {
                    return;
                }

                _sourceIdentity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SourceStatusDisplay));
            }
        }

        public string SourceStatusDisplay => SourceState == SourceState.Idle
            ? SourceState.ToDisplayString()
            : $"{SourceIdentity ?? "Source"} · {SourceState.ToDisplayString()}";

        #endregion


        #region Scanner panel

        // Definition.

        public string ScannerIdentityDisplay => string.IsNullOrWhiteSpace(_scannerDefinition.Mark)
                ? _scannerDefinition.Name
                : $"{_scannerDefinition.Name} · {_scannerDefinition.Mark}";
        public string BoardNameDisplay => _scannerDefinition.MicrocontrollerBoard.Name;
        public string MicrocontrollerDisplay => _scannerDefinition.MicrocontrollerBoard.Microcontroller;
        public string SensorNameDisplay => _scannerDefinition.RangeSensor.Name;
        public string SensorRangeDisplay => $"{_scannerDefinition.RangeSensor.MinRangeCm:0.##} – {_scannerDefinition.RangeSensor.MaxRangeCm:0.##} cm";
        public string SensorMeasuringAngleDisplay => $"{_scannerDefinition.RangeSensor.MeasuringAngleDegrees:0.##}°";
        public string ServoNameDisplay => _scannerDefinition.ServoMotor.Name;
        public string ServoRotationRangeDisplay => $"{_scannerDefinition.ServoMotor.RotationRangeDegrees:0.##}°";

        // Serial connection.

        public ObservableCollection<string> AvailablePortNames { get; } = [];

        public string SerialBaudRateDisplay => $"{DefaultBaudRate} baud";

        // Simulation.

        #region Simulation

        public SimulationScenario[] AvailableSimulationScenarios { get; } = Enum.GetValues<SimulationScenario>();

        private SimulationScenario _selectedSimulationScenario = SimulationScenario.SymmetricSweep;
        public SimulationScenario SelectedSimulationScenario
        {
            get => _selectedSimulationScenario;
            private set
            {
                if (_selectedSimulationScenario == value)
                {
                    return;
                }

                _selectedSimulationScenario = value;
                OnPropertyChanged();
            }
        }

        #endregion

        // Scanner configuration.

        private double _minBearingDegrees;
        public double MinBearingDegrees
        {
            get => _minBearingDegrees;
            set
            {
                if (_minBearingDegrees == value)
                {
                    return;
                }

                _minBearingDegrees = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(SweepRangeDisplay));
            }
        }

        private double _maxBearingDegrees;
        public double MaxBearingDegrees
        {
            get => _maxBearingDegrees;
            set
            {
                if (_maxBearingDegrees == value)
                {
                    return;
                }

                _maxBearingDegrees = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(SweepRangeDisplay));
            }
        }

        private double _bearingStepDegrees;
        public double BearingStepDegrees
        {
            get => _bearingStepDegrees;
            set
            {
                if (_bearingStepDegrees == value)
                {
                    return;
                }

                _bearingStepDegrees = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(BearingStepDisplay));
            }
        }

        private uint _acquisitionDelayMs;
        public uint AcquisitionDelayMs
        {
            get => _acquisitionDelayMs;
            set
            {
                if (_acquisitionDelayMs == value)
                {
                    return;
                }

                _acquisitionDelayMs = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(AcquisitionDelayDisplay));
            }
        }

        private uint _echoTimeoutUs;
        public uint EchoTimeoutUs
        {
            get => _echoTimeoutUs;
            set
            {
                if (_echoTimeoutUs == value)
                {
                    return;
                }

                _echoTimeoutUs = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(EchoTimeoutDisplay));
            }
        }

        // Formatted configuration.

        public string SweepRangeDisplay => $"{MinBearingDegrees:0.##}° ↔ {MaxBearingDegrees:0.##}°";
        public string BearingStepDisplay => $"{BearingStepDegrees:0.##}°";
        public string AcquisitionDelayDisplay => $"{AcquisitionDelayMs} ms";
        public string EchoTimeoutDisplay => $"{EchoTimeoutUs} μs";

        #endregion


        #region Inspector panel

        // Sample.

        private string _sequenceDisplay = "-";
        public string SequenceDisplay
        {
            get => _sequenceDisplay;
            set
            {
                _sequenceDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _sweepIdDisplay = "-";
        public string SweepIdDisplay
        {
            get => _sweepIdDisplay;
            set
            {
                _sweepIdDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _elapsedDisplay = "-";
        public string ElapsedDisplay
        {
            get => _elapsedDisplay;
            set
            {
                _elapsedDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _bearingDisplay = "-";
        public string BearingDisplay
        {
            get => _bearingDisplay;
            set
            {
                _bearingDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _roundTripDisplay = "-";
        public string RoundTripDisplay
        {
            get => _roundTripDisplay;
            set
            {
                _roundTripDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _statusDisplay = "-";
        public string StatusDisplay
        {
            get => _statusDisplay;
            set
            {
                _statusDisplay = value;
                OnPropertyChanged();
            }
        }

        // Observation.

        private string _elapsedSecondsDisplay = "-";
        public string ElapsedSecondsDisplay
        {
            get => _elapsedSecondsDisplay;
            set
            {
                _elapsedSecondsDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _distanceDisplay = "-";
        public string DistanceDisplay
        {
            get => _distanceDisplay;
            set
            {
                _distanceDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _positionXDisplay = "-";
        public string PositionXDisplay
        {
            get => _positionXDisplay;
            set
            {
                _positionXDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _positionYDisplay = "-";
        public string PositionYDisplay
        {
            get => _positionYDisplay;
            set
            {
                _positionYDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _rangeStatusDisplay = "-";
        public string RangeStatusDisplay
        {
            get => _rangeStatusDisplay;
            set
            {
                _rangeStatusDisplay = value;
                OnPropertyChanged();
            }
        }

        #endregion


        #region Status Bar

        // Mouse position.

        private Point? _latestMousePositionCm;

        private string _mousePositionDisplay = "Undefined";
        public string MousePositionDisplay
        {
            get => _mousePositionDisplay;
            set
            {
                _mousePositionDisplay = value;
                OnPropertyChanged();
            }
        }

        #endregion


        #endregion


        #region Rendering

        // Viewport geometry.

        private const double ViewportHeightCm = 1200;
        private const int StrokeThicknessSmall = 1;
        private const int StrokeThicknessMedium = 2;

        private const double MajorGridSpacingCm = 100;
        private const double MinorRulerTickOffsetCm = MajorGridSpacingCm / 2;

        private const double MajorRulerTickLengthPixels = 10;
        private const double MinorRulerTickLengthPixels = 5;

        private const double RulerThicknessPixels = 30;
        private const double RulerEdgePaddingPixels = 8;

        private const double ScannerMarkerDiameterPixels = 10;
        private const double ScannerForwardMarkerLengthPixels = 7;
        private const double ObservationMarkerRadiusPixels = 4;

        private readonly record struct ViewportTransform(
            double Left,
            double Top,
            double Width,
            double Height,
            double OriginScreenX,
            double OriginScreenY,
            double CmToPixels
        )
        {
            public double WorldToScreenX(double worldX)
            {
                return OriginScreenX + worldX * CmToPixels;
            }

            public double WorldToScreenY(double worldY)
            {
                return OriginScreenY - worldY * CmToPixels;
            }

            public double ScreenToWorldX(double screenX)
            {
                return (screenX - OriginScreenX) / CmToPixels;
            }

            public double ScreenToWorldY(double screenY)
            {
                return (OriginScreenY - screenY) / CmToPixels;
            }
        }

        private readonly record struct GridLayout(
            double FirstX,
            double FirstY,
            int VerticalCount,
            int HorizontalCount
        );

        // Brushes.

        private readonly Brush _viewportGridLineBrush;
        private readonly Brush _viewportRulerBackgroundBrush;
        private readonly Brush _viewportRulerLineBrush;
        private readonly Brush _viewportRulerTextBrush;
        private readonly Brush _viewportObservationBrush;
        private readonly Brush _viewportObservationErrorBrush;
        private readonly Brush _viewportRangeStrokeBrush;
        private readonly Brush _viewportRangeFillBrush;

        // Grid layer.

        private readonly List<Line> _horizontalGridLines = [];
        private readonly List<Line> _verticalGridLines = [];

        private readonly List<TextBlock> _horizontalGridRulerLabels = [];
        private readonly List<TextBlock> _verticalGridRulerLabels = [];

        private readonly List<Line> _horizontalMajorTicks = [];
        private readonly List<Line> _horizontalMinorTicks = [];

        private readonly List<Line> _verticalMajorTicks = [];
        private readonly List<Line> _verticalMinorTicks = [];

        private readonly Rectangle _rulerBackgroundRectangleHorizontal;
        private readonly Rectangle _rulerBackgroundRectangleVertical;
        private readonly Rectangle _rulerBackgroundRectangleCorner;

        private readonly Line _rulerBorderLineHorizontal;
        private readonly Line _rulerBorderLineVertical;
        private readonly Line _rulerBorderLineDiagonal;

        private readonly Rectangle _canvasBorderRectangle;

        // Gizmo layer.

        private readonly Ellipse _scannerOriginMarker = new();
        private readonly Line _scannerForwardMarker = new();

        private readonly Line _observationLine = new();
        private readonly Ellipse _observationEndpointEllipse = new();

        private readonly System.Windows.Shapes.Path _layoutScannerCoveragePath = new();
        private readonly PathGeometry _layoutScannerCoveragePathGeometry = new();
        private readonly PathFigure _layoutScannerCoveragePathFigure = new() { IsClosed = true };
        private readonly LineSegment _layoutScannerCoverageMinBearingLineSegment = new();
        private readonly ArcSegment _layoutScannerCoverageOuterArcSegment = new();
        private readonly LineSegment _layoutScannerCoverageMaxBearingLineSegment = new();
        private readonly ArcSegment _layoutScannerCoverageInnerArcSegment = new();

        #endregion


        #endregion

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _viewportGridLineBrush = (Brush)FindResource("ViewportGridLineBrush");
            _viewportRulerBackgroundBrush = (Brush)FindResource("ViewportRulerBackgroundBrush");
            _viewportRulerLineBrush = (Brush)FindResource("ViewportRulerLineBrush");
            _viewportRulerTextBrush = (Brush)FindResource("ViewportRulerTextBrush");
            _viewportObservationBrush = (Brush)FindResource("ViewportObservationBrush");
            _viewportObservationErrorBrush = (Brush)FindResource("ViewportObservationErrorBrush");
            _viewportRangeStrokeBrush = (Brush)FindResource("ViewportRangeStrokeBrush");
            _viewportRangeFillBrush = (Brush)FindResource("ViewportRangeFillBrush");

            _rulerBackgroundRectangleHorizontal = new() { Fill = _viewportRulerBackgroundBrush };
            _rulerBackgroundRectangleVertical = new() { Fill = _viewportRulerBackgroundBrush };
            _rulerBackgroundRectangleCorner = new() { Fill = _viewportRulerBackgroundBrush };

            _rulerBorderLineHorizontal = new() { Stroke = _viewportRulerLineBrush, StrokeThickness = StrokeThicknessSmall };
            _rulerBorderLineVertical = new() { Stroke = _viewportRulerLineBrush, StrokeThickness = StrokeThicknessSmall };
            _rulerBorderLineDiagonal = new() { Stroke = _viewportRulerLineBrush, StrokeThickness = StrokeThicknessSmall };

            _canvasBorderRectangle = new() { Stroke = _viewportRulerLineBrush, StrokeThickness = StrokeThicknessSmall };

            _rangeSampleSerializerService = new();

            RefreshSerialPorts();
            SelectPreferredSerialPort();

            SimulationScenarioComboBox.SelectedItem = SelectedSimulationScenario;

            MinBearingDegrees = _scannerConfiguration.MinBearingDegrees;
            MaxBearingDegrees = _scannerConfiguration.MaxBearingDegrees;
            BearingStepDegrees = _scannerConfiguration.BearingStepDegrees;
            AcquisitionDelayMs = _scannerConfiguration.AcquisitionDelayMs;
            EchoTimeoutUs = _scannerConfiguration.EchoTimeoutUs;

            Loaded += MainWindow_Loaded;

            ViewportHost.SizeChanged += ViewportHost_SizeChanged;
            ViewportHost.MouseMove += ViewportHost_MouseMove;
            ViewportHost.MouseLeave += ViewportHost_MouseLeave;

            InitializeGizmoLayer();
        }

        #region Window / UI

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateWindowFrame();
            LayoutViewportForCurrentSize();
        }

        protected override void OnClosed(EventArgs e)
        {
            _ = StopInputProcessingAsync();

            base.OnClosed(e);
        }

        #endregion


        #region Transport

        private void RefreshSerialPorts()
        {
            var serialPortNames = SerialPort.GetPortNames();

            AvailablePortNames.Clear();
            AvailablePortNames.AddRange(serialPortNames);

            if (_serialPort.IsOpen && !serialPortNames.Contains(_serialPort.PortName))
            {
                _ = StopInputProcessingAsync();
                CloseSerialPort();
            }

            if (_selectedSerialPortName != DefaultSerialPortName && !serialPortNames.Contains(_selectedSerialPortName))
            {
                _selectedSerialPortName = DefaultSerialPortName;

                return;
            }

            ValidPortsComboBox.SelectedIndex = serialPortNames.IndexOf(_selectedSerialPortName);
        }

        private void SelectPreferredSerialPort()
        {
            if (_selectedSerialPortName == DefaultSerialPortName && AvailablePortNames.Count > 0)
            {
                if (AvailablePortNames.Contains(PreferredSerialPortName))
                {
                    _selectedSerialPortName = PreferredSerialPortName;
                }
                else
                {
                    _selectedSerialPortName = AvailablePortNames.First();
                }

                ValidPortsComboBox.SelectedIndex = AvailablePortNames.IndexOf(_selectedSerialPortName);
            }
        }

        private bool OpenSerialPort(string portName, int baudRate)
        {
            try
            {
                _serialPort.PortName = portName;
                _serialPort.BaudRate = baudRate;
                _serialPort.Open();

                return true;
            }
            catch (Exception ex)
            {
                UpdateError(ex.Message);

                return false;
            }
        }

        private void CloseSerialPort()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            _serialPort.PortName = DefaultSerialPortName;
            _serialPort.BaudRate = DefaultBaudRate;
        }

        #endregion


        #region Processing

        private async Task CleanupFailedInputAsync(Stream stream)
        {
            if (!ReferenceEquals(_inputProcessingStream, stream))
            {
                return;
            }

            _inputProcessingStream = null;
            _inputProcessingTask = null;

            SourceState = SourceState.Idle;
            SourceType = SourceType.None;
            SourceIdentity = null;

            var cancellationTokenSource = _inputProcessingCancellationTokenSource;
            _inputProcessingCancellationTokenSource = null;

            await CloseInputTransportAsync(stream);

            cancellationTokenSource?.Dispose();
        }

        private async Task PerformHandshakeAsync(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken)
        {
            await writer.WriteLineAsync("HELLO");

            var timeout = TimeSpan.FromSeconds(3);
            const string timeoutMessage = "Scanner handshake timed out. Check the connection and try again.";
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                var remaining = timeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                string? response;
                try
                {
                    response = await reader
                        .ReadLineAsync(cancellationToken)
                        .AsTask()
                        .WaitAsync(remaining, cancellationToken);
                }
                catch (TimeoutException ex)
                {
                    throw new TimeoutException(timeoutMessage, ex);
                }

                if (response is null)
                {
                    throw new EndOfStreamException("The scanner disconnected during the handshake.");
                }

                if (response == "HELLO BACK")
                {
                    return;
                }

                // We ignore stale or pre-handshake inputs.
            }

            throw new TimeoutException(timeoutMessage);
        }

        private async Task ProcessSamplesAsync(StreamReader reader, ScannerSession session, CancellationToken cancellationToken)
        {
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                // Protocol.
                if (!_rangeSampleSerializerService.TryDeserialize(line, out var rangeSample))
                {
                    continue;
                }

                // Domain.
                var distanceCm = CalculateDistanceCm(rangeSample.RoundTripDurationUs);
                var rangeStatus = GetRangeStatus(distanceCm);

                var observation = new RangeObservation(rangeSample, distanceCm, rangeStatus);
                session.AddObservation(observation);

                // Presentation / Rendering.
                await Dispatcher.InvokeAsync(() =>
                {
                    var transform = GetViewportTransform();

                    AddObservationPoint(transform, observation);
                    ShowLatestObservationIndicator(transform, observation);

                    UpdateSampleDisplay(rangeSample);
                    UpdateObservationDisplay(observation);
                });
            }
        }

        private async Task ProcessInputAsync(Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                using StreamReader reader = new(stream, leaveOpen: true);
                await using StreamWriter writer = new(stream, leaveOpen: true)
                {
                    AutoFlush = true,
                    NewLine = "\r\n"
                };

                await PerformHandshakeAsync(reader, writer, cancellationToken);

                ActiveSession = new();

                await writer.WriteLineAsync("START");

                SourceState = SourceState.Active;

                await ProcessSamplesAsync(reader, ActiveSession, cancellationToken);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (!cancellationToken.IsCancellationRequested && ReferenceEquals(_inputProcessingStream, stream))
                    {
                        SourceState = SourceState.Idle;
                        SourceIdentity = null;
                    }
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is the expected completion path when changing inputs or closing.
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                // Closing a stream is part of the expected cancellation path.
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                await CleanupFailedInputAsync(stream);

                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateError(ex.Message);
                });
            }
        }

        private void StartInputProcessing(Stream stream, SourceType sourceType, string sourceIdentity)
        {
            _inputProcessingCancellationTokenSource = new();
            _inputProcessingStream = stream;

            SourceType = sourceType;
            SourceIdentity = sourceIdentity;
            SourceState = SourceState.Connecting;

            UpdateError(null);

            ObjectLayer.Children.Clear();
            HideLatestObservationIndicator();

            UpdateSampleDisplay(null);
            UpdateObservationDisplay(null);

            _inputProcessingTask = ProcessInputAsync(stream, _inputProcessingCancellationTokenSource.Token);
        }

        private async Task CloseInputTransportAsync(Stream? stream)
        {
            if (stream is null)
            {
                return;
            }

            if (stream is ScannerSimulationStream)
            {
                await stream.DisposeAsync();
                return;
            }

            CloseSerialPort();
        }

        private async Task StopInputProcessingAsync()
        {
            // Snapshot the previous session to make sure cleanup cannot target the replacement.
            var task = _inputProcessingTask;
            var cancellationTokenSource = _inputProcessingCancellationTokenSource;
            var stream = _inputProcessingStream;

            _inputProcessingTask = null;
            _inputProcessingCancellationTokenSource = null;
            _inputProcessingStream = null;

            SourceState = SourceState.Idle;
            SourceType = SourceType.None;
            SourceIdentity = null;

            UpdateError(null);

            if (task is null)
            {
                return;
            }

            cancellationTokenSource?.Cancel();

            try
            {
                if (stream is ScannerSimulationStream)
                {
                    await task;
                    await CloseInputTransportAsync(stream);
                }
                else
                {
                    await CloseInputTransportAsync(stream);
                    await task;
                }
            }
            catch (Exception ex) when (
                ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // Cancellation and transport shutdown are expected during cleanup.
            }
            finally
            {
                cancellationTokenSource?.Dispose();
            }
        }

        #endregion


        #region Domain

        private double CalculateDistanceCm(uint roundTripDurationUs)
        {
            // Convert round trip pulse duration in microseconds to distance in cm.
            return roundTripDurationUs * 0.034 / 2;
        }

        private RangeStatus GetRangeStatus(double distanceCm)
        {
            if (distanceCm > _scannerDefinition.RangeSensor.MaxRangeCm)
            {
                return RangeStatus.TooFar;
            }
            else if (distanceCm < _scannerDefinition.RangeSensor.MinRangeCm)
            {
                return RangeStatus.TooClose;
            }
            else
            {
                return RangeStatus.InRange;
            }
        }

        #endregion


        #region Presentation

        // TODO: Remove once viewmodels are introduced.
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new(propertyName));
        }


        #region Scanner panel

        // Serial connection.

        private async Task ConnectSerialAsync()
        {
            await StopInputProcessingAsync();
            CloseSerialPort();

            if (_selectedSerialPortName == DefaultSerialPortName)
            {
                UpdateError("Please select a serial port");
                return;
            }

            if (!OpenSerialPort(_selectedSerialPortName, DefaultBaudRate))
            {
                return;
            }

            StartInputProcessing(_serialPort.BaseStream, SourceType.Serial, $"Serial · {_selectedSerialPortName}");
        }

        private void RefreshSerialPortsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshSerialPorts();
            SelectPreferredSerialPort();
        }

        private async void SerialConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            switch (SourceState)
            {
                case SourceState.Idle:
                    await ConnectSerialAsync();
                    break;

                case SourceState.Connecting:
                case SourceState.Active:
                    await StopInputProcessingAsync();
                    break;
            }
        }

        private void ValidPortsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedValue is string selectedValue)
            {
                _selectedSerialPortName = selectedValue;
            }
        }

        // Simulation.

        #region Simulation

        private async Task StartSimulationAsync()
        {
            await StopInputProcessingAsync();
            CloseSerialPort();

            var stream = new ScannerSimulationStream(
                ScannerSimulationGenerator.Create(SelectedSimulationScenario, _scannerConfiguration),
                _rangeSampleSerializerService
            );

            StartInputProcessing(stream, SourceType.Simulation, "Simulation");
        }

        private async void SimulationButton_Click(object sender, RoutedEventArgs e)
        {
            switch (SourceState)
            {
                case SourceState.Idle:
                    await StartSimulationAsync();
                    break;

                case SourceState.Connecting:
                case SourceState.Active:
                    await StopInputProcessingAsync();
                    break;
            }
        }

        private void SimulationScenarioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is SimulationScenario scenario)
            {
                SelectedSimulationScenario = scenario;
            }
        }

        #endregion


        // Session.

        private void ClearSessionButton_Click(object sender, RoutedEventArgs e)
        {
            ActiveSession?.Clear();
            ActiveSession = null;

            ObjectLayer.Children.Clear();
            HideLatestObservationIndicator();

            UpdateSampleDisplay(null);
            UpdateObservationDisplay(null);
        }

        // Error.

        private void UpdateError(string? message)
        {
            ErrorMessageTextBlock.Text = message ?? string.Empty;
            ErrorMessageBorder.Visibility = string.IsNullOrWhiteSpace(message)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        #endregion


        #region Inspector panel

        private void UpdateSampleDisplay(RangeSample? rangeSample)
        {
            SequenceDisplay = rangeSample.HasValue
                ? $"{rangeSample.Value.Sequence}"
                : "-";
            SweepIdDisplay = rangeSample.HasValue
                ? $"{rangeSample.Value.SweepId}"
                : "-";
            ElapsedDisplay = rangeSample.HasValue
                ? $"{rangeSample.Value.ElapsedUs} μs"
                : "-";
            BearingDisplay = rangeSample.HasValue
                ? $"{rangeSample.Value.BearingDegrees:F2} °"
                : "-";
            RoundTripDisplay = rangeSample.HasValue
                ? $"{rangeSample.Value.RoundTripDurationUs} μs"
                : "-";
            StatusDisplay = rangeSample.HasValue
                ? rangeSample.Value.Status.ToDisplayString()
                : "-";
        }

        private void UpdateObservationDisplay(RangeObservation? rangeObservation)
        {
            ElapsedSecondsDisplay = rangeObservation.HasValue
                ? $"{rangeObservation.Value.ElapsedSeconds:F2} s"
                : "-";
            DistanceDisplay = rangeObservation.HasValue
                ? $"{rangeObservation.Value.DistanceCm:F2} cm"
                : "-";
            PositionXDisplay = rangeObservation.HasValue
                ? $"{rangeObservation.Value.PositionXCm:F2} cm"
                : "-";
            PositionYDisplay = rangeObservation.HasValue
                ? $"{rangeObservation.Value.PositionYCm:F2} cm"
                : "-";
            RangeStatusDisplay = rangeObservation.HasValue
                ? rangeObservation.Value.RangeStatus.ToDisplayString()
                : "-";
        }

        #endregion


        #region Status Bar

        private void UpdateMousePosition()
        {
            if (_latestMousePositionCm is not Point point)
            {
                MousePositionDisplay = "Undefined";

                return;
            }

            MousePositionDisplay = $"{(int)point.X} × {(int)point.Y} cm";
        }

        #endregion


        #endregion


        #region Rendering

        private ViewportTransform CalculateViewportTransform(
            double canvasWidth,
            double canvasHeight
        )
        {
            var left = RulerThicknessPixels;
            var top = RulerThicknessPixels;

            var width = canvasWidth - left;
            var height = canvasHeight - top;

            var originScreenX = left + width / 2;
            var originScreenY = top + height / 2;

            var cmToPixels = height / ViewportHeightCm;

            return new(left, top, width, height, originScreenX, originScreenY, cmToPixels);
        }

        private ViewportTransform GetViewportTransform() => CalculateViewportTransform(ViewportHost.ActualWidth, ViewportHost.ActualHeight);

        private GridLayout CalculateGridLayout(ViewportTransform transform)
        {
            var minWorldX = transform.ScreenToWorldX(transform.Left);
            var maxWorldX = transform.ScreenToWorldX(transform.Left + transform.Width);

            var maxWorldY = transform.ScreenToWorldY(transform.Top);
            var minWorldY = transform.ScreenToWorldY(transform.Top + transform.Height);

            var firstGridX = Math.Ceiling(minWorldX / MajorGridSpacingCm) * MajorGridSpacingCm;
            var lastGridX = Math.Floor(maxWorldX / MajorGridSpacingCm) * MajorGridSpacingCm;

            var firstGridY = Math.Ceiling(minWorldY / MajorGridSpacingCm) * MajorGridSpacingCm;
            var lastGridY = Math.Floor(maxWorldY / MajorGridSpacingCm) * MajorGridSpacingCm;

            var horizontalCount = (int)((lastGridY - firstGridY) / MajorGridSpacingCm) + 1;
            var verticalCount = (int)((lastGridX - firstGridX) / MajorGridSpacingCm) + 1;

            return new(firstGridX, firstGridY, verticalCount, horizontalCount);
        }

        private (double Width, double Height) GetTextBlockSize(TextBlock textBlock)
        {
            // https://learn.microsoft.com/en-us/answers/questions/400490/how-to-get-the-actual-width-of-textblock
            var formatted = new FormattedText(
                textBlock.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize, textBlock.Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip
            );

            return (formatted.Width, formatted.Height);
        }

        private void PopulateGridLayer(GridLayout layout)
        {
            GridLayer.Children.Clear();

            _horizontalGridLines.Clear();
            _verticalGridLines.Clear();

            _horizontalGridRulerLabels.Clear();
            _verticalGridRulerLabels.Clear();

            _horizontalMajorTicks.Clear();
            _horizontalMinorTicks.Clear();

            _verticalMajorTicks.Clear();
            _verticalMinorTicks.Clear();


            for (int i = 0; i < layout.HorizontalCount; i++)
            {
                _horizontalGridLines.Add(new()
                {
                    Stroke = _viewportGridLineBrush,
                    StrokeThickness = StrokeThicknessSmall
                });

                _horizontalGridRulerLabels.Add(new()
                {
                    Foreground = _viewportRulerTextBrush
                });

                _horizontalMajorTicks.Add(new()
                {
                    Stroke = _viewportRulerLineBrush,
                    StrokeThickness = StrokeThicknessSmall
                });
            }

            var horizontalMinorTickCount = Math.Max(0, layout.HorizontalCount - 1);
            for (int i = 0; i < horizontalMinorTickCount; i++)
            {
                _horizontalMinorTicks.Add(new()
                {
                    Stroke = _viewportRulerLineBrush,
                    StrokeThickness = StrokeThicknessSmall
                });
            }

            for (int i = 0; i < layout.VerticalCount; i++)
            {
                _verticalGridLines.Add(new()
                {
                    Stroke = _viewportGridLineBrush,
                    StrokeThickness = StrokeThicknessSmall
                });

                _verticalGridRulerLabels.Add(new()
                {
                    Foreground = _viewportRulerTextBrush
                });

                _verticalMajorTicks.Add(new()
                {
                    Stroke = _viewportRulerLineBrush,
                    StrokeThickness = StrokeThicknessSmall
                });
            }

            var verticalMinorTickCount = Math.Max(0, layout.VerticalCount - 1);
            for (int i = 0; i < verticalMinorTickCount; i++)
            {
                _verticalMinorTicks.Add(new()
                {
                    Stroke = _viewportRulerLineBrush,
                    StrokeThickness = StrokeThicknessSmall
                });
            }

            // Grid border.
            GridLayer.Children.Add(_canvasBorderRectangle);

            // Ruler background.
            GridLayer.Children.Add(_rulerBackgroundRectangleHorizontal);
            GridLayer.Children.Add(_rulerBackgroundRectangleVertical);
            GridLayer.Children.Add(_rulerBackgroundRectangleCorner);

            // Ruler Borders.
            GridLayer.Children.Add(_rulerBorderLineHorizontal);
            GridLayer.Children.Add(_rulerBorderLineVertical);
            GridLayer.Children.Add(_rulerBorderLineDiagonal);

            // Grid lines.
            foreach (var line in _horizontalGridLines)
            {
                GridLayer.Children.Add(line);
            }

            foreach (var line in _verticalGridLines)
            {
                GridLayer.Children.Add(line);
            }

            // Ruler labels.
            foreach (var label in _horizontalGridRulerLabels)
            {
                GridLayer.Children.Add(label);
            }

            foreach (var label in _verticalGridRulerLabels)
            {
                GridLayer.Children.Add(label);
            }

            // Ruler ticks.
            foreach (var line in _horizontalMajorTicks)
            {
                GridLayer.Children.Add(line);
            }

            foreach (var line in _horizontalMinorTicks)
            {
                GridLayer.Children.Add(line);
            }

            foreach (var line in _verticalMajorTicks)
            {
                GridLayer.Children.Add(line);
            }

            foreach (var line in _verticalMinorTicks)
            {
                GridLayer.Children.Add(line);
            }
        }

        private void LayoutGridLines(ViewportTransform transform, GridLayout layout)
        {
            _canvasBorderRectangle.Width = transform.Width + transform.Left;
            _canvasBorderRectangle.Height = transform.Height + transform.Top;

            Canvas.SetLeft(_canvasBorderRectangle, 0);
            Canvas.SetTop(_canvasBorderRectangle, 0);

            for (var i = 0; i < layout.VerticalCount; i++)
            {
                var worldX = layout.FirstX + i * MajorGridSpacingCm;
                var screenX = transform.WorldToScreenX(worldX);

                var line = _verticalGridLines[i];

                line.X1 = screenX;
                line.Y1 = transform.Top;
                line.X2 = screenX;
                line.Y2 = transform.Top + transform.Height;
            }

            for (var j = 0; j < layout.HorizontalCount; j++)
            {
                var worldY = layout.FirstY + j * MajorGridSpacingCm;
                var screenY = transform.WorldToScreenY(worldY);

                var line = _horizontalGridLines[j];

                line.X1 = transform.Left;
                line.Y1 = screenY;
                line.X2 = transform.Width + transform.Left;
                line.Y2 = screenY;
            }
        }

        private void LayoutRulers(ViewportTransform transform, GridLayout layout)
        {
            // Ruler background.
            _rulerBackgroundRectangleHorizontal.Width = transform.Width;
            _rulerBackgroundRectangleHorizontal.Height = transform.Top;

            Canvas.SetLeft(_rulerBackgroundRectangleHorizontal, transform.Left);
            Canvas.SetTop(_rulerBackgroundRectangleHorizontal, 0);

            _rulerBackgroundRectangleVertical.Width = transform.Left;
            _rulerBackgroundRectangleVertical.Height = transform.Height;

            Canvas.SetLeft(_rulerBackgroundRectangleVertical, 0);
            Canvas.SetTop(_rulerBackgroundRectangleVertical, transform.Top);

            _rulerBackgroundRectangleCorner.Width = transform.Left;
            _rulerBackgroundRectangleCorner.Height = transform.Top;

            Canvas.SetLeft(_rulerBackgroundRectangleCorner, 0);
            Canvas.SetTop(_rulerBackgroundRectangleCorner, 0);

            // Ruler borders.
            _rulerBorderLineHorizontal.X1 = 0;
            _rulerBorderLineHorizontal.Y1 = transform.Top;
            _rulerBorderLineHorizontal.X2 = transform.Width + transform.Left;
            _rulerBorderLineHorizontal.Y2 = transform.Top;

            _rulerBorderLineVertical.X1 = transform.Left;
            _rulerBorderLineVertical.Y1 = 0;
            _rulerBorderLineVertical.X2 = transform.Left;
            _rulerBorderLineVertical.Y2 = transform.Height + transform.Top;

            _rulerBorderLineDiagonal.X1 = 0;
            _rulerBorderLineDiagonal.Y1 = 0;
            _rulerBorderLineDiagonal.X2 = transform.Left;
            _rulerBorderLineDiagonal.Y2 = transform.Top;

            for (var i = 0; i < layout.VerticalCount; i++)
            {
                var worldX = layout.FirstX + i * MajorGridSpacingCm;
                var screenX = transform.WorldToScreenX(worldX);

                var label = _verticalGridRulerLabels[i];
                label.Text = $"{worldX} cm";

                var (labelWidth, _) = GetTextBlockSize(label);
                var labelLeft = screenX - labelWidth / 2;
                var labelRight = screenX + labelWidth / 2;

                Canvas.SetLeft(label, labelLeft);
                Canvas.SetTop(label, 0);

                var tick = _verticalMajorTicks[i];

                tick.X1 = screenX;
                tick.Y1 = transform.Top;
                tick.X2 = screenX;
                tick.Y2 = transform.Top - MajorRulerTickLengthPixels;

                // Make sure that ticks and labels are not shown when close to the edges of the ruler.
                var rulerLeft = transform.Left;
                var rulerRight = transform.Left + transform.Width;

                var markFitsHorizontally =
                    screenX >= rulerLeft + RulerEdgePaddingPixels &&
                    screenX <= rulerRight - RulerEdgePaddingPixels &&
                    labelLeft >= rulerLeft &&
                    labelRight <= rulerRight;

                label.Visibility = markFitsHorizontally ? Visibility.Visible : Visibility.Collapsed;
                tick.Visibility = markFitsHorizontally ? Visibility.Visible : Visibility.Collapsed;
            }

            for (var j = 0; j < layout.HorizontalCount; j++)
            {
                var worldY = layout.FirstY + j * MajorGridSpacingCm;
                var screenY = transform.WorldToScreenY(worldY);

                var label = _horizontalGridRulerLabels[j];
                label.Text = $"{worldY} cm";

                var (labelWidth, _) = GetTextBlockSize(label);
                var labelTop = screenY - labelWidth / 2;
                var labelBottom = screenY + labelWidth / 2;

                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, labelTop);

                label.LayoutTransform = new RotateTransform(270);

                var tick = _horizontalMajorTicks[j];

                tick.X1 = transform.Left;
                tick.Y1 = screenY;
                tick.X2 = transform.Left - MajorRulerTickLengthPixels;
                tick.Y2 = screenY;

                var rulerTop = transform.Top;
                var rulerBottom = transform.Top + transform.Height;

                var markFitsVertically =
                    screenY >= rulerTop + RulerEdgePaddingPixels &&
                    screenY <= rulerBottom - RulerEdgePaddingPixels &&
                    labelTop >= rulerTop &&
                    labelBottom <= rulerBottom;

                label.Visibility = markFitsVertically ? Visibility.Visible : Visibility.Collapsed;
                tick.Visibility = markFitsVertically ? Visibility.Visible : Visibility.Collapsed;
            }

            var verticalMinorTickCount = Math.Max(0, layout.VerticalCount - 1);
            for (var i = 0; i < verticalMinorTickCount; i++)
            {
                var worldX = layout.FirstX + i * MajorGridSpacingCm + MinorRulerTickOffsetCm;
                var screenX = transform.WorldToScreenX(worldX);

                var tick = _verticalMinorTicks[i];

                tick.X1 = screenX;
                tick.Y1 = transform.Top;
                tick.X2 = screenX;
                tick.Y2 = transform.Top - MinorRulerTickLengthPixels;

                // Make sure that ticks are not shown when close to the edges of the ruler.
                var rulerLeft = transform.Left;
                var rulerRight = transform.Left + transform.Width;

                var markFitsHorizontally =
                    screenX >= rulerLeft + RulerEdgePaddingPixels &&
                    screenX <= rulerRight - RulerEdgePaddingPixels;

                tick.Visibility = markFitsHorizontally ? Visibility.Visible : Visibility.Collapsed;
            }

            var horizontalMinorTickCount = Math.Max(0, layout.HorizontalCount - 1);
            for (var j = 0; j < horizontalMinorTickCount; j++)
            {
                var worldY = layout.FirstY + j * MajorGridSpacingCm + MinorRulerTickOffsetCm;
                var screenY = transform.WorldToScreenY(worldY);

                var tick = _horizontalMinorTicks[j];

                tick.X1 = transform.Left;
                tick.Y1 = screenY;
                tick.X2 = transform.Left - MinorRulerTickLengthPixels;
                tick.Y2 = screenY;

                var rulerTop = transform.Top;
                var rulerBottom = transform.Top + transform.Height;

                var markFitsVertically =
                    screenY >= rulerTop + RulerEdgePaddingPixels &&
                    screenY <= rulerBottom - RulerEdgePaddingPixels;

                tick.Visibility = markFitsVertically ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void InitializeGizmoLayer()
        {
            _scannerOriginMarker.Width = ScannerMarkerDiameterPixels;
            _scannerOriginMarker.Height = ScannerMarkerDiameterPixels;
            _scannerOriginMarker.Fill = _viewportRulerBackgroundBrush;
            _scannerOriginMarker.Stroke = _viewportObservationBrush;
            _scannerOriginMarker.StrokeThickness = StrokeThicknessMedium;

            _scannerForwardMarker.Stroke = _viewportObservationBrush;
            _scannerForwardMarker.StrokeThickness = StrokeThicknessMedium;

            _observationLine.Visibility = Visibility.Visible;
            _observationEndpointEllipse.Visibility = Visibility.Visible;

            _layoutScannerCoveragePathFigure.Segments.Add(_layoutScannerCoverageMinBearingLineSegment);
            _layoutScannerCoveragePathFigure.Segments.Add(_layoutScannerCoverageOuterArcSegment);
            _layoutScannerCoveragePathFigure.Segments.Add(_layoutScannerCoverageMaxBearingLineSegment);
            _layoutScannerCoveragePathFigure.Segments.Add(_layoutScannerCoverageInnerArcSegment);

            _layoutScannerCoveragePathGeometry.Figures.Add(_layoutScannerCoveragePathFigure);

            _layoutScannerCoveragePath.Data = _layoutScannerCoveragePathGeometry;
            _layoutScannerCoveragePath.Stroke = _viewportRangeStrokeBrush;
            _layoutScannerCoveragePath.StrokeThickness = StrokeThicknessMedium;
            _layoutScannerCoveragePath.StrokeDashArray = [1, 2];
            _layoutScannerCoveragePath.Fill = _viewportRangeFillBrush;

            GizmoLayer.Children.Add(_layoutScannerCoveragePath);

            GizmoLayer.Children.Add(_observationLine);
            GizmoLayer.Children.Add(_observationEndpointEllipse);

            GizmoLayer.Children.Add(_scannerForwardMarker);
            GizmoLayer.Children.Add(_scannerOriginMarker);
        }

        private Point GetScreenPointAtBearing(ViewportTransform transform, double bearingDegrees, double rangeCm)
        {
            var bearingRadians = double.DegreesToRadians(bearingDegrees);

            return new(
                transform.WorldToScreenX(Math.Sin(bearingRadians) * rangeCm),
                transform.WorldToScreenY(Math.Cos(bearingRadians) * rangeCm)
            );
        }
        private void LayoutScannerMarker(ViewportTransform transform)
        {
            var markerRadius = _scannerOriginMarker.Width / 2.0;

            Canvas.SetLeft(_scannerOriginMarker, transform.OriginScreenX - markerRadius);
            Canvas.SetTop(_scannerOriginMarker, transform.OriginScreenY - markerRadius);

            _scannerForwardMarker.X1 = transform.OriginScreenX;
            _scannerForwardMarker.Y1 = transform.OriginScreenY - markerRadius;

            _scannerForwardMarker.X2 = transform.OriginScreenX;
            _scannerForwardMarker.Y2 = transform.OriginScreenY - markerRadius - ScannerForwardMarkerLengthPixels;
        }

        private void LayoutScannerCoverage(ViewportTransform transform)
        {
            var minRangeCm = _scannerDefinition.RangeSensor.MinRangeCm;
            var maxRangeCm = _scannerDefinition.RangeSensor.MaxRangeCm;

            var minRadiusPixels = minRangeCm * transform.CmToPixels;
            var maxRadiusPixels = maxRangeCm * transform.CmToPixels;

            var minBearingInnerPoint = GetScreenPointAtBearing(transform, MinBearingDegrees, minRangeCm);
            var minBearingOuterPoint = GetScreenPointAtBearing(transform, MinBearingDegrees, maxRangeCm);

            var maxBearingOuterPoint = GetScreenPointAtBearing(transform, MaxBearingDegrees, maxRangeCm);
            var maxBearingInnerPoint = GetScreenPointAtBearing(transform, MaxBearingDegrees, minRangeCm);

            var sweepAngleDegrees = MaxBearingDegrees - MinBearingDegrees;
            var isLargeArc = sweepAngleDegrees > 180.0;

            _layoutScannerCoveragePathFigure.StartPoint = minBearingInnerPoint;

            _layoutScannerCoverageMinBearingLineSegment.Point = minBearingOuterPoint;

            _layoutScannerCoverageOuterArcSegment.Point = maxBearingOuterPoint;
            _layoutScannerCoverageOuterArcSegment.Size = new(maxRadiusPixels, maxRadiusPixels);
            _layoutScannerCoverageOuterArcSegment.SweepDirection = SweepDirection.Clockwise;
            _layoutScannerCoverageOuterArcSegment.IsLargeArc = isLargeArc;

            _layoutScannerCoverageMaxBearingLineSegment.Point = maxBearingInnerPoint;

            _layoutScannerCoverageInnerArcSegment.Point = minBearingInnerPoint;
            _layoutScannerCoverageInnerArcSegment.Size = new(minRadiusPixels, minRadiusPixels);
            _layoutScannerCoverageInnerArcSegment.SweepDirection = SweepDirection.Counterclockwise;
            _layoutScannerCoverageInnerArcSegment.IsLargeArc = isLargeArc;
        }

        private void ShowLatestObservationIndicator(
            ViewportTransform transform,
            RangeObservation observation
        )
        {
            _observationLine.Visibility = Visibility.Visible;
            _observationEndpointEllipse.Visibility = Visibility.Visible;

            var x = transform.WorldToScreenX(observation.PositionXCm);
            var y = transform.WorldToScreenY(observation.PositionYCm);

            _observationLine.X1 = transform.OriginScreenX;
            _observationLine.Y1 = transform.OriginScreenY;
            _observationLine.X2 = x;
            _observationLine.Y2 = y;

            var stroke = observation.Sample.Status == SampleStatus.Valid && observation.RangeStatus == RangeStatus.InRange
                ? _viewportObservationBrush
                : _viewportObservationErrorBrush;

            _observationLine.Stroke = stroke;

            _observationEndpointEllipse.Width = ObservationMarkerRadiusPixels * 2;
            _observationEndpointEllipse.Height = ObservationMarkerRadiusPixels * 2;
            _observationEndpointEllipse.Fill = stroke;

            Canvas.SetLeft(_observationEndpointEllipse, _observationLine.X2 - ObservationMarkerRadiusPixels);
            Canvas.SetTop(_observationEndpointEllipse, _observationLine.Y2 - ObservationMarkerRadiusPixels);
        }

        private void HideLatestObservationIndicator()
        {
            _observationLine.Visibility = Visibility.Collapsed;
            _observationEndpointEllipse.Visibility = Visibility.Collapsed;
        }

        private void AddObservationPoint(
            ViewportTransform transform,
            RangeObservation observation
        )
        {
            var stroke = observation.Sample.Status == SampleStatus.Valid && observation.RangeStatus == RangeStatus.InRange
                ? _viewportObservationBrush
                : _viewportObservationErrorBrush;

            var ellipse = new Ellipse()
            {
                Width = ObservationMarkerRadiusPixels * 2,
                Height = ObservationMarkerRadiusPixels * 2,
                Fill = stroke
            };

            var x = transform.WorldToScreenX(observation.PositionXCm);
            var y = transform.WorldToScreenY(observation.PositionYCm);

            Canvas.SetLeft(ellipse, x - ObservationMarkerRadiusPixels);
            Canvas.SetTop(ellipse, y - ObservationMarkerRadiusPixels);

            ObjectLayer.Children.Add(ellipse);

            _observationLine.Visibility = Visibility.Collapsed;
            _observationEndpointEllipse.Visibility = Visibility.Collapsed;
        }

        private void PopulateObservationLayer(ViewportTransform transform)
        {
            ObjectLayer.Children.Clear();

            if (ActiveSession is null)
            {
                return;
            }

            foreach (var observation in ActiveSession.Observations)
            {
                AddObservationPoint(transform, observation);
            }
        }

        private void LayoutViewportForCurrentSize()
        {
            var transform = GetViewportTransform();
            var gridLayout = CalculateGridLayout(transform);

            PopulateGridLayer(gridLayout);

            LayoutGridLines(transform, gridLayout);
            LayoutRulers(transform, gridLayout);

            LayoutScannerCoverage(transform);
            LayoutScannerMarker(transform);

            PopulateObservationLayer(transform);

            if (ActiveSession?.LatestObservation is RangeObservation observation)
            {
                ShowLatestObservationIndicator(transform, observation);
            }
            else
            {
                HideLatestObservationIndicator();
            }
        }

        private void ViewportHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            LayoutViewportForCurrentSize();
        }

        private void ViewportHost_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var position = e.GetPosition(ViewportHost);
            var transform = GetViewportTransform();

            if (position.X < transform.Left ||
                position.Y < transform.Top ||
                position.X > transform.Left + transform.Width ||
                position.Y > transform.Top + transform.Height
            )
            {
                _latestMousePositionCm = null;
                UpdateMousePosition();

                return;
            }

            var x = position.X;
            var y = position.Y;

            var worldX = transform.ScreenToWorldX(x);
            var worldY = transform.ScreenToWorldY(y);

            _latestMousePositionCm = new(worldX, worldY);
            UpdateMousePosition();
        }

        private void ViewportHost_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _latestMousePositionCm = null;
            UpdateMousePosition();
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
