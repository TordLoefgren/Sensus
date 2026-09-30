using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using Sensus.Models;
using Sensus.Models.Enums;
using SkiaSharp;

namespace Sensus.Views
{
    public partial class TimelineView : UserControl
    {

        #region Chart Data and State

        private const double WindowSeconds = 15;

        private readonly ObservableCollection<ObservablePoint> _distance = [];
        private readonly ObservableCollection<ObservablePoint> _bearing = [];
        private readonly ObservableCollection<ObservablePoint> _status = [];

        private bool _isObserving;

        #endregion

        #region View Properties

        public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
            nameof(Session),
            typeof(ScannerSession),
            typeof(TimelineView),
            new FrameworkPropertyMetadata(null, OnSessionChanged)
        );

        public static readonly DependencyProperty ScannerDefinitionProperty = DependencyProperty.Register(
            nameof(ScannerDefinition),
            typeof(ScannerDefinition?),
            typeof(TimelineView),
            new FrameworkPropertyMetadata(null, OnScannerGeometryChanged)
        );

        public static readonly DependencyProperty ScannerConfigurationProperty = DependencyProperty.Register(
            nameof(ScannerConfiguration),
            typeof(ScannerConfiguration?),
            typeof(TimelineView),
            new FrameworkPropertyMetadata(null, OnScannerGeometryChanged)
        );

        public ScannerSession? Session
        {
            get => (ScannerSession?)GetValue(SessionProperty);
            set => SetValue(SessionProperty, value);
        }

        public ScannerDefinition? ScannerDefinition
        {
            get => (ScannerDefinition?)GetValue(ScannerDefinitionProperty);
            set => SetValue(ScannerDefinitionProperty, value);
        }

        public ScannerConfiguration? ScannerConfiguration
        {
            get => (ScannerConfiguration?)GetValue(ScannerConfigurationProperty);
            set => SetValue(ScannerConfigurationProperty, value);
        }

        #endregion

        #region Series and Axes

        public ISeries[] DistanceSeries { get; }
        public ISeries[] BearingSeries { get; }
        public ISeries[] StatusSeries { get; }

        public Axis[] DistanceTimeAxes { get; }
        public Axis[] BearingTimeAxes { get; }
        public Axis[] StatusTimeAxes { get; }

        public Axis[] DistanceAxes { get; }
        public Axis[] BearingAxes { get; }
        public Axis[] StatusAxes { get; }

        #endregion

        #region Initialization

        private static Axis[] CreateTimeAxes(SKColor textColor, bool showLabels) =>
        [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = WindowSeconds,
                Labeler = value => $"{value:0.#} s",

                // Keep tick spacing stable while the visible window scrolls.
                MinStep = 2,
                ForceStepToMin = true,
                TextSize = 11,
                IsVisible = showLabels,
                LabelsPaint = new SolidColorPaint(textColor),
                ShowSeparatorLines = false,
                SeparatorsPaint = null
            }
        ];

        public TimelineView()
        {
            var textColor = GetChartColor("TextBrushSecondary");
            var lineColor = GetChartColor("TimelineSeriesBrush");
            var gridColor = GetChartColor("TimelineGridLineBrush");

            // Keep axis limits synchronized across the charts, with one visible time axis.
            DistanceTimeAxes = CreateTimeAxes(textColor, showLabels: false);
            BearingTimeAxes = CreateTimeAxes(textColor, showLabels: false);
            StatusTimeAxes = CreateTimeAxes(textColor, showLabels: true);

            DistanceSeries =
            [
                new LineSeries<ObservablePoint>
                {
                    Name = "Distance",
                    Values = _distance,
                    Fill = null,
                    GeometrySize = 0,
                    LineSmoothness = 0,
                    Stroke = new SolidColorPaint(lineColor, 1.25f)
                }
            ];
            BearingSeries =
            [
                new LineSeries<ObservablePoint>
                {
                    Name = "Bearing",
                    Values = _bearing,
                    Fill = null,
                    GeometrySize = 0,
                    LineSmoothness = 0,

                    // Keep the full stroke at the configured extrema. Clip only to the time window.
                    ClippingMode = ClipMode.X,
                    Stroke = new SolidColorPaint(lineColor, 1.25f)
                }
            ];
            StatusSeries =
            [
                new StepLineSeries<ObservablePoint>
                {
                    Name = "Status",
                    Values = _status,
                    Fill = null,
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(lineColor, 1.5f)
                }
            ];
            DistanceAxes =
            [
                new Axis
                {
                    MinLimit = 0,
                    TextSize = 11,

                    // Allow fewer ticks on short plots instead of enforcing a minimum count.
                    MinSeparators = 0,
                    Labeler = value => $"{value:0} cm",
                    LabelsPaint = new SolidColorPaint(textColor),
                    SeparatorsPaint = new SolidColorPaint(gridColor),
                    IsVisible = false
                }
            ];
            BearingAxes =
            [
                new Axis
                {
                    TextSize = 11,
                    Labeler = value => $"{value:0}°",
                    LabelsPaint = new SolidColorPaint(textColor),
                    SeparatorsPaint = new SolidColorPaint(gridColor),
                    IsVisible = false
                }
            ];
            StatusAxes =
            [
                new Axis
                {
                    TextSize = 11,
                    MinLimit = -0.1,
                    MaxLimit = 1.1,
                    CustomSeparators = [0, 1],
                    Labeler = value => value == 1 ? "Valid" : "No echo",
                    LabelsPaint = new SolidColorPaint(textColor),
                    ShowSeparatorLines = false,
                    SeparatorsPaint = null,
                    IsVisible = false
                }
            ];

            InitializeComponent();

            UpdateValueAxes();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private SKColor GetChartColor(string resourceKey)
        {
            // Derive LiveCharts Skia colors from the shared WPF palette.
            var brush = (System.Windows.Media.SolidColorBrush)FindResource(resourceKey);
            var color = brush.Color;

            return new SKColor(color.R, color.G, color.B, (byte)(color.A * brush.Opacity));
        }

        #endregion

        #region Value Axis Scaling

        private static void OnScannerGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TimelineView)d).UpdateValueAxes();
        }

        private void UpdateValueAxisVisibility()
        {
            var hasObservations = _bearing.Count > 0;

            DistanceAxes[0].IsVisible = hasObservations;
            BearingAxes[0].IsVisible = hasObservations;
            StatusAxes[0].IsVisible = hasObservations;
        }

        private void UpdateValueAxes()
        {
            // Start with the expected distance range, then include the measurements in the current window.
            DistanceAxes[0].MinLimit = 0;
            DistanceAxes[0].MaxLimit = ScannerDefinition?.RangeSensor.MaxRangeCm;

            foreach (var point in _distance)
            {
                if (point.Y is { } distance)
                {
                    ExpandDistanceRange(distance);
                }
            }

            UpdateDistanceSeparators();

            BearingAxes[0].MinLimit = ScannerConfiguration?.MinBearingDegrees;
            BearingAxes[0].MaxLimit = ScannerConfiguration?.MaxBearingDegrees;

            UpdateBearingSeparators();
        }

        private void UpdateDistanceSeparators()
        {
            var axis = DistanceAxes[0];

            if (axis.MinLimit is { } min && axis.MaxLimit is { } max)
            {
                axis.CustomSeparators = [min, max];
            }
        }

        private void UpdateBearingSeparators()
        {
            if (ScannerConfiguration is not { } configuration)
            {
                BearingAxes[0].CustomSeparators = null;
                return;
            }

            var min = configuration.MinBearingDegrees;
            var max = configuration.MaxBearingDegrees;

            BearingAxes[0].CustomSeparators =
            [
                min,
                (min + max) / 2,
                max
            ];
        }

        private void ExpandDistanceRange(double distance)
        {
            if (!double.IsFinite(distance))
            {
                return;
            }

            // Only expand the scale. Reset it when the session is cleared or rebuilt.
            var axis = DistanceAxes[0];
            var min = Math.Min(axis.MinLimit ?? 0, distance);
            var max = Math.Max(axis.MaxLimit ?? distance, distance);
            if (axis.MinLimit == min && axis.MaxLimit == max)
            {
                return;
            }

            axis.MinLimit = min;
            axis.MaxLimit = max;

            UpdateDistanceSeparators();
        }

        #endregion

        #region View Lifecycle and Session Events

        private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (TimelineView)d;
            if (!view._isObserving)
            {
                return;
            }

            if (e.OldValue is ScannerSession previous)
            {
                CollectionChangedEventManager.RemoveHandler(previous.Observations, view.OnObservationsChanged);
            }

            if (e.NewValue is ScannerSession current)
            {
                CollectionChangedEventManager.AddHandler(current.Observations, view.OnObservationsChanged);
            }

            view.RebuildWindow();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isObserving)
            {
                return;
            }

            _isObserving = true;

            DistanceChart.UpdateFinished += OnChartUpdateFinished;
            StatusChart.UpdateFinished += OnChartUpdateFinished;
            TimelineSurface.SizeChanged += OnTimelineSizeChanged;

            if (Session is { } session)
            {
                CollectionChangedEventManager.AddHandler(session.Observations, OnObservationsChanged);
            }

            RebuildWindow();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isObserving = false;

            DistanceChart.UpdateFinished -= OnChartUpdateFinished;
            StatusChart.UpdateFinished -= OnChartUpdateFinished;
            TimelineSurface.SizeChanged -= OnTimelineSizeChanged;

            HideCursor();

            if (Session is { } session)
            {
                CollectionChangedEventManager.RemoveHandler(session.Observations, OnObservationsChanged);
            }
        }

        private void OnObservationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Acquisition resumes on the UI thread, just as it does for the scan.
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } observations)
            {
                foreach (RangeObservation observation in observations)
                {
                    AppendObservation(observation);
                }
            }
            else
            {
                RebuildWindow();
            }
        }

        #endregion

        #region Rolling Observation Window

        private void RebuildWindow()
        {
            _distance.Clear();
            _bearing.Clear();
            _status.Clear();

            UpdateValueAxisVisibility();

            HideCursor();

            UpdateValueAxes();
            UpdateTimeWindow(0);

            if (Session is not { } session || session.Observations.Count == 0)
            {
                return;
            }

            // Reopening the view copies only the visible tail. The session keeps its full history.
            var observations = session.Observations;
            var first = observations.Count - 1;
            var cutoff = observations[^1].ElapsedSeconds - WindowSeconds;
            while (first > 0 && observations[first - 1].ElapsedSeconds >= cutoff &&
                observations[first - 1].ElapsedSeconds <= observations[first].ElapsedSeconds
            )
            {
                first--;
            }

            for (var i = first; i < observations.Count; i++)
            {
                AppendObservation(observations[i]);
            }
        }

        private void AppendObservation(RangeObservation observation)
        {
            var seconds = observation.ElapsedSeconds;
            // The device's uint microsecond clock can wrap (or restart).
            if (_distance.Count > 0 && seconds < _distance[^1].X)
            {
                _distance.Clear();
                _bearing.Clear();
                _status.Clear();
            }

            var valid = observation.Sample.Status == SampleStatus.Valid;
            // A null Y creates a gap instead of plotting a timeout as zero distance.
            _distance.Add(new(seconds, valid ? observation.DistanceCm : null));
            _bearing.Add(new(seconds, observation.Sample.BearingDegrees));
            _status.Add(new(seconds, valid ? 1 : 0));

            UpdateValueAxisVisibility();

            if (valid)
            {
                ExpandDistanceRange(observation.DistanceCm);
            }

            var cutoff = Math.Max(0, seconds - WindowSeconds);
            while (_distance.Count > 0 && _distance[0].X < cutoff)
            {
                _distance.RemoveAt(0);
                _bearing.RemoveAt(0);
                _status.RemoveAt(0);
            }

            UpdateTimeWindow(seconds);
        }

        private void UpdateTimeWindow(double seconds)
        {
            foreach (var axes in new[] { DistanceTimeAxes, BearingTimeAxes, StatusTimeAxes })
            {
                axes[0].MinLimit = Math.Max(0, seconds - WindowSeconds);
                axes[0].MaxLimit = Math.Max(WindowSeconds, seconds);
            }
        }

        #endregion

        #region Timeline Cursor and Tooltip

        private bool TryGetPlotBounds(CartesianChart chart, out Rect bounds)
        {
            bounds = Rect.Empty;

            if (!chart.IsLoaded || chart.CoreChart is not { } core)
            {
                return false;
            }

            var location = core.DrawMarginLocation;
            var size = core.DrawMarginSize;
            if (!double.IsFinite(location.X) ||
                !double.IsFinite(location.Y) ||
                !double.IsFinite(size.Width) ||
                !double.IsFinite(size.Height) ||
                size.Width <= 0 ||
                size.Height <= 0
            )
            {
                return false;
            }

            var topLeft = chart.TranslatePoint(
                new(location.X, location.Y),
                TimelineSurface
            );
            var bottomRight = chart.TranslatePoint(
                new(location.X + size.Width, location.Y + size.Height),
                TimelineSurface
            );

            bounds = new(topLeft, bottomRight);

            return true;
        }

        private bool TryGetTimelineTime(Point position, out double seconds, out Rect bounds)
        {
            seconds = 0;
            bounds = Rect.Empty;

            if (!TryGetPlotBounds(DistanceChart, out var distanceBounds) ||
                !TryGetPlotBounds(StatusChart, out var statusBounds) ||
                statusBounds.Bottom <= distanceBounds.Top
            )
            {
                return false;
            }

            // One continuous inspection region includes the gaps between tracks.
            bounds = new(
                distanceBounds.Left,
                distanceBounds.Top,
                distanceBounds.Width,
                statusBounds.Bottom - distanceBounds.Top
            );
            if (!bounds.Contains(position))
            {
                return false;
            }

            var chartPosition = TimelineSurface.TranslatePoint(position, DistanceChart);
            seconds = DistanceChart.ScalePixelsToData(new(chartPosition.X, chartPosition.Y)).X;

            return double.IsFinite(seconds);
        }

        private void UpdateCursor(Point position)
        {
            if (!TryGetTimelineTime(position, out var seconds, out var bounds))
            {
                HideCursor();
                return;
            }

            TimelineCursor.X1 = TimelineCursor.X2 = position.X;
            TimelineCursor.Y1 = bounds.Top;
            TimelineCursor.Y2 = bounds.Bottom;
            TimelineCursor.Visibility = Visibility.Visible;

            TimelineCursorTime.Text = $"{seconds:0.00} s";
            TimelineCursorTooltip.Visibility = Visibility.Visible;
            TimelineCursorTooltip.Measure(new(double.PositiveInfinity, double.PositiveInfinity));

            var tooltipSize = TimelineCursorTooltip.DesiredSize;
            const double gap = 8;
            var left = position.X + gap;
            if (left + tooltipSize.Width > TimelineSurface.ActualWidth)
            {
                left = position.X - gap - tooltipSize.Width;
            }

            Canvas.SetLeft(
                TimelineCursorTooltip,
                Math.Clamp(left, 0, Math.Max(0, TimelineSurface.ActualWidth - tooltipSize.Width))
            );
            Canvas.SetTop(
                TimelineCursorTooltip,
                Math.Clamp(position.Y + gap, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - tooltipSize.Height))
            );
        }

        private void HideCursor()
        {
            TimelineCursor.Visibility = Visibility.Collapsed;
            TimelineCursorTooltip.Visibility = Visibility.Collapsed;
        }

        private void RefreshCursor()
        {
            if (_isObserving && TimelineSurface.IsMouseOver)
            {
                UpdateCursor(Mouse.GetPosition(TimelineSurface));
            }
            else
            {
                HideCursor();
            }
        }

        private void OnChartUpdateFinished(LiveChartsCore.Kernel.Sketches.IChartView chart)
        {
            // LiveCharts may finish updates off the UI thread. Read geometry after its update.
            Dispatcher.InvokeAsync(RefreshCursor);
        }

        private void OnTimelineSizeChanged(object sender, SizeChangedEventArgs e) => RefreshCursor();

        private void OnTimelineMouseMove(object sender, MouseEventArgs e) => UpdateCursor(e.GetPosition(TimelineSurface));

        private void OnTimelineMouseLeave(object sender, MouseEventArgs e) => HideCursor();

        #endregion

    }
}
