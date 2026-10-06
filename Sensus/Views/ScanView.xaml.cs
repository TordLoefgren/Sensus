using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Sensus.Models;
using Sensus.Models.Enums;

namespace Sensus.Views
{
    /// <summary>
    /// Interaction logic for ScanView.xaml
    /// </summary>
    public partial class ScanView : UserControl
    {

        #region Layout Constants

        private const double DefaultScanHeightCm = 500;
        private const double CoveragePaddingPixels = 32;
        private const double MinZoom = 1.0;
        private const double MaxZoom = 32.0;
        private const double ZoomFactor = 1.25;
        private const int StrokeThicknessSmall = 1;
        private const int StrokeThicknessMedium = 2;
        private const double GridSpacingVeryFineCm = 2;
        private const double GridSpacingExtraFineCm = 5;
        private const double GridSpacingCloseCm = 10;
        private const double GridSpacingFineCm = 25;
        private const double GridSpacingMediumCm = 50;
        private const double GridSpacingCoarseCm = 100;
        private const double MajorRulerTickLengthPixels = 10;
        private const double MinorRulerTickLengthPixels = 5;
        private const double RulerThicknessPixels = 30;
        private const double RulerEdgePaddingPixels = 8;
        private const double RulerLabelGapPixels = 8;
        private const double PolarBearingStepDegrees = 30;
        private const double PolarLabelOffsetPixels = 12;
        private const double ScannerMarkerDiameterPixels = 10;
        private const double ScannerForwardMarkerLengthPixels = 7;
        private const double ObservationMarkerRadiusPixels = 2.5;
        private const double LatestObservationMarkerRadiusPixels = 4;

        #endregion

        #region Rendering Brushes

        private readonly Brush _scanGridLineBrush;
        private readonly Brush _scanRulerBackgroundBrush;
        private readonly Brush _scanRulerLineBrush;
        private readonly Brush _scanAnnotationBrush;
        private readonly Brush _scanObservationBrush;
        private readonly Brush _scanObservationErrorBrush;
        private readonly Brush _scanCoverageOutlineBrush;
        private readonly Brush _scanCoverageFillBrush;

        #endregion

        #region Drawing Elements

        // Cartesian grid and rulers.

        private readonly List<Line> _horizontalCartesianGridLines = [];
        private readonly List<Line> _verticalCartesianGridLines = [];
        private readonly List<TextBlock> _horizontalRulerLabels = [];
        private readonly List<TextBlock> _verticalRulerLabels = [];
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

        // Polar grid lines and foreground labels.

        private readonly List<Line> _polarBearingLines = [];
        private readonly List<TextBlock> _polarBearingLabels = [];

        // Observations, coverage, and scanner indicators.

        private readonly List<Ellipse> _observationMarkers = [];
        private readonly Path _coverageFillPath = new();
        private readonly Ellipse _scannerOriginMarker = new();
        private readonly Line _scannerForwardMarker = new();
        private readonly Line _observationLine = new();
        private readonly Ellipse _observationEndpointEllipse = new();
        private readonly Path _layoutScannerCoveragePath = new();
        private readonly PathGeometry _layoutScannerCoveragePathGeometry = new();
        private readonly PathFigure _layoutScannerCoveragePathFigure = new() { IsClosed = true };
        private readonly LineSegment _layoutScannerCoverageMinBearingLineSegment = new();
        private readonly ArcSegment _layoutScannerCoverageOuterArcSegment = new();
        private readonly LineSegment _layoutScannerCoverageMaxBearingLineSegment = new();
        private readonly ArcSegment _layoutScannerCoverageInnerArcSegment = new();

        #endregion

        #region View Properties and State

        public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
            nameof(Session),
            typeof(ScannerSession),
            typeof(ScanView),
            new FrameworkPropertyMetadata(null, OnSessionChanged)
        );

        public static readonly DependencyProperty ScannerDefinitionProperty = DependencyProperty.Register(
            nameof(ScannerDefinition),
            typeof(ScannerDefinition?),
            typeof(ScanView),
            new FrameworkPropertyMetadata(null, OnScannerGeometryChanged)
        );

        public static readonly DependencyProperty ScannerConfigurationProperty = DependencyProperty.Register(
            nameof(ScannerConfiguration),
            typeof(ScannerConfiguration?),
            typeof(ScanView),
            new FrameworkPropertyMetadata(null, OnScannerGeometryChanged)
        );

        public static readonly DependencyProperty MousePositionCmProperty = DependencyProperty.Register(
            nameof(MousePositionCm),
            typeof(Point?),
            typeof(ScanView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
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

        public Point? MousePositionCm
        {
            get => (Point?)GetValue(MousePositionCmProperty);
            set => SetValue(MousePositionCmProperty, value);
        }

        private bool _isRendering;
        private ViewportTransform _viewportTransform;

        private double _zoom = 1.0;

        #endregion

        #region Initialization

        public ScanView()
        {
            InitializeComponent();

            _scanGridLineBrush = (Brush)FindResource("ScanGridLineBrush");
            _scanRulerBackgroundBrush = (Brush)FindResource("ScanRulerBackgroundBrush");
            _scanRulerLineBrush = (Brush)FindResource("ScanRulerLineBrush");
            _scanAnnotationBrush = (Brush)FindResource("ScanAnnotationBrush");
            _scanObservationBrush = (Brush)FindResource("ScanObservationBrush");
            _scanObservationErrorBrush = (Brush)FindResource("ScanObservationErrorBrush");
            _scanCoverageOutlineBrush = (Brush)FindResource("ScanCoverageOutlineBrush");
            _scanCoverageFillBrush = (Brush)FindResource("ScanCoverageFillBrush");

            _rulerBackgroundRectangleHorizontal = new() { Fill = _scanRulerBackgroundBrush };
            _rulerBackgroundRectangleVertical = new() { Fill = _scanRulerBackgroundBrush };
            _rulerBackgroundRectangleCorner = new() { Fill = _scanRulerBackgroundBrush };

            _rulerBorderLineHorizontal = new() { Stroke = _scanRulerLineBrush, StrokeThickness = StrokeThicknessSmall };
            _rulerBorderLineVertical = new() { Stroke = _scanRulerLineBrush, StrokeThickness = StrokeThicknessSmall };
            _rulerBorderLineDiagonal = new() { Stroke = _scanRulerLineBrush, StrokeThickness = StrokeThicknessSmall };

            _canvasBorderRectangle = new() { Stroke = _scanRulerLineBrush, StrokeThickness = StrokeThicknessSmall };

            InitializeRulerLayer();
            InitializeIndicatorLayer();

            Loaded += ScanView_Loaded;
            Unloaded += ScanView_Unloaded;

            ScanHost.SizeChanged += ScanHost_SizeChanged;
            ScanHost.MouseMove += ScanHost_MouseMove;
            ScanHost.MouseLeave += ScanHost_MouseLeave;
            ScanHost.MouseWheel += ScanHost_MouseWheel;
        }

        private void InitializeRulerLayer()
        {
            RulerLayer.Children.Add(_canvasBorderRectangle);
            RulerLayer.Children.Add(_rulerBackgroundRectangleHorizontal);
            RulerLayer.Children.Add(_rulerBackgroundRectangleVertical);
            RulerLayer.Children.Add(_rulerBackgroundRectangleCorner);
            RulerLayer.Children.Add(_rulerBorderLineHorizontal);
            RulerLayer.Children.Add(_rulerBorderLineVertical);
            RulerLayer.Children.Add(_rulerBorderLineDiagonal);
        }

        private void InitializeIndicatorLayer()
        {
            _scannerOriginMarker.Width = ScannerMarkerDiameterPixels;
            _scannerOriginMarker.Height = ScannerMarkerDiameterPixels;
            _scannerOriginMarker.Fill = _scanRulerBackgroundBrush;
            _scannerOriginMarker.Stroke = _scanObservationBrush;
            _scannerOriginMarker.StrokeThickness = StrokeThicknessMedium;

            _scannerForwardMarker.Stroke = _scanObservationBrush;
            _scannerForwardMarker.StrokeThickness = StrokeThicknessMedium;

            _observationLine.Visibility = Visibility.Visible;
            _observationEndpointEllipse.Visibility = Visibility.Visible;

            _layoutScannerCoveragePathFigure.Segments.Add(_layoutScannerCoverageMinBearingLineSegment);
            _layoutScannerCoveragePathFigure.Segments.Add(_layoutScannerCoverageOuterArcSegment);
            _layoutScannerCoveragePathFigure.Segments.Add(_layoutScannerCoverageMaxBearingLineSegment);
            _layoutScannerCoveragePathFigure.Segments.Add(_layoutScannerCoverageInnerArcSegment);

            _layoutScannerCoveragePathGeometry.Figures.Add(_layoutScannerCoveragePathFigure);

            _layoutScannerCoveragePath.Data = _layoutScannerCoveragePathGeometry;
            _layoutScannerCoveragePath.Stroke = _scanCoverageOutlineBrush;
            _layoutScannerCoveragePath.StrokeThickness = StrokeThicknessMedium;
            _layoutScannerCoveragePath.StrokeDashArray = [1, 2];

            // Share geometry while drawing the fill behind data and the outline above it.
            _coverageFillPath.Data = _layoutScannerCoveragePathGeometry;
            _coverageFillPath.Fill = _scanCoverageFillBrush;

            CoverageLayer.Children.Add(_coverageFillPath);

            IndicatorLayer.Children.Add(_layoutScannerCoveragePath);

            IndicatorLayer.Children.Add(_observationLine);
            IndicatorLayer.Children.Add(_observationEndpointEllipse);

            IndicatorLayer.Children.Add(_scannerForwardMarker);
            IndicatorLayer.Children.Add(_scannerOriginMarker);
        }

        #endregion

        #region View Lifecycle and Session Events

        private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (ScanView)d;
            view._zoom = MinZoom;

            if (!view._isRendering)
            {
                return;
            }

            // A dependency-property callback reports replacement, not collection mutations.
            if (e.OldValue is ScannerSession previous)
            {
                CollectionChangedEventManager.RemoveHandler(previous.Observations, view.OnObservationsChanged);
            }

            if (e.NewValue is ScannerSession current)
            {
                CollectionChangedEventManager.AddHandler(current.Observations, view.OnObservationsChanged);
            }

            view.LayoutScanForCurrentSize();
        }

        private static void OnScannerGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (ScanView)d;
            if (view._isRendering)
            {
                view.LayoutScanForCurrentSize();
            }
        }

        private void ScanView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isRendering)
            {
                return;
            }

            _isRendering = true;

            if (Session is { } session)
            {
                CollectionChangedEventManager.AddHandler(session.Observations, OnObservationsChanged);
            }

            LayoutScanForCurrentSize();
        }

        private void ScanView_Unloaded(object sender, RoutedEventArgs e)
        {
            _isRendering = false;

            if (Session is { } session)
            {
                CollectionChangedEventManager.RemoveHandler(session.Observations, OnObservationsChanged);
            }

            SetCurrentValue(MousePositionCmProperty, null);
        }

        private void OnObservationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var transform = _viewportTransform;
            if (transform.Width <= 0 || transform.Height <= 0)
            {
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } newItems)
            {
                foreach (RangeObservation observation in newItems)
                {
                    if (observation.DistanceCm.HasValue)
                    {
                        AddObservationPoint(transform, observation);
                    }
                }
            }
            else
            {
                LayoutObservations(transform);
            }

            UpdateLatestObservationIndicator(transform);
        }

        private void ScanHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            LayoutScanForCurrentSize();
        }

        #endregion

        #region Mouse Input

        private void ScanHost_MouseMove(object sender, MouseEventArgs e) => UpdateMousePosition(e.GetPosition(ScanHost));

        private void ScanHost_MouseLeave(object sender, MouseEventArgs e) => SetCurrentValue(MousePositionCmProperty, null);

        private void ScanHost_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            _zoom = Math.Clamp(
                _zoom * Math.Pow(ZoomFactor, e.Delta / 120.0),
                MinZoom,
                MaxZoom
            );

            LayoutScanForCurrentSize();

            e.Handled = true;
        }

        private void UpdateMousePosition(Point position)
        {
            var transform = _viewportTransform;
            if (transform.Width <= 0 || transform.Height <= 0 ||
                position.X < transform.Left || position.Y < transform.Top ||
                position.X > transform.Left + transform.Width || position.Y > transform.Top + transform.Height
            )
            {
                SetCurrentValue(MousePositionCmProperty, null);

                return;
            }

            SetCurrentValue(
                MousePositionCmProperty,
                new Point(transform.ScreenToWorldX(position.X), transform.ScreenToWorldY(position.Y))
            );
        }

        #endregion

        #region Layout and Coverage Fitting

        private void LayoutScanForCurrentSize()
        {
            if (!_isRendering)
            {
                return;
            }

            var transform = CalculateViewportTransform(ScanHost.ActualWidth, ScanHost.ActualHeight);
            _viewportTransform = transform;

            // All plot layers share the transform's drawable area. Rulers stay outside it.
            var plotClip = new RectangleGeometry(GetPlotBounds(transform));
            plotClip.Freeze();

            PlotLayers.Clip = plotClip;

            if (transform.Width <= 0 || transform.Height <= 0)
            {
                SetCurrentValue(MousePositionCmProperty, null);
                return;
            }

            var gridLayout = CalculateCartesianGridLayout(transform);

            EnsureCartesianGridElements(gridLayout);

            LayoutCartesianGridLines(transform, gridLayout);
            LayoutRulers(transform, gridLayout);

            LayoutPolarGrid(transform);
            LayoutScannerCoverage(transform);
            LayoutScannerMarker(transform);

            LayoutObservations(transform);

            UpdateLatestObservationIndicator(transform);

            if (ScanHost.IsMouseOver)
            {
                UpdateMousePosition(Mouse.GetPosition(ScanHost));
            }
            else
            {
                SetCurrentValue(MousePositionCmProperty, null);
            }
        }

        private static Rect GetPlotBounds(ViewportTransform transform) =>
            new(transform.Left, transform.Top, Math.Max(0, transform.Width), Math.Max(0, transform.Height));

        private static ViewportTransform FitCoverage(
            Rect plotBounds,
            double rangeCm,
            double minBearingDegrees,
            double maxBearingDegrees,
            double paddingPixels
        )
        {
            // Include the scanner origin, both sweep endpoints, and any arc extrema.
            var coverage = new Rect(0, 0, 0, 0);

            void IncludeBearing(double bearing)
            {
                var radians = double.DegreesToRadians(bearing);
                coverage.Union(new Point(Math.Sin(radians) * rangeCm, Math.Cos(radians) * rangeCm));
            }

            IncludeBearing(minBearingDegrees);
            IncludeBearing(maxBearingDegrees);

            var firstCardinal = Math.Ceiling(minBearingDegrees / 90) * 90;
            for (var i = 0; i < 4 && firstCardinal + i * 90 <= maxBearingDegrees; i++)
            {
                IncludeBearing(firstCardinal + i * 90);
            }

            // Leave room for polar labels and markers, even in a small panel.
            var padding = Math.Min(paddingPixels, Math.Min(plotBounds.Width, plotBounds.Height) / 4);

            var scaleX = coverage.Width > 0 ? (plotBounds.Width - 2 * padding) / coverage.Width : double.PositiveInfinity;
            var scaleY = coverage.Height > 0 ? (plotBounds.Height - 2 * padding) / coverage.Height : double.PositiveInfinity;

            var scale = Math.Min(scaleX, scaleY);

            var centerX = coverage.Left + coverage.Width / 2;
            var centerY = coverage.Top + coverage.Height / 2;

            return new(
                plotBounds.Left,
                plotBounds.Top,
                plotBounds.Width,
                plotBounds.Height,
                plotBounds.Left + plotBounds.Width / 2 - centerX * scale,
                plotBounds.Top + plotBounds.Height / 2 + centerY * scale, scale
            );
        }

        private ViewportTransform CalculateViewportTransform(
            double canvasWidth,
            double canvasHeight
        )
        {
            var left = RulerThicknessPixels;
            var top = RulerThicknessPixels;

            var width = Math.Max(0, canvasWidth - left);
            var height = Math.Max(0, canvasHeight - top);

            if (width > 0 && height > 0 &&
                ScannerDefinition is { } definition && ScannerConfiguration is { } configuration &&
                double.IsFinite(definition.RangeSensor.MaxRangeCm) && definition.RangeSensor.MaxRangeCm > 0 &&
                double.IsFinite(configuration.MinBearingDegrees) && double.IsFinite(configuration.MaxBearingDegrees) &&
                configuration.MaxBearingDegrees >= configuration.MinBearingDegrees
            )
            {
                var fitted = FitCoverage(
                    new(left, top, width, height),
                    definition.RangeSensor.MaxRangeCm,
                    configuration.MinBearingDegrees,
                    configuration.MaxBearingDegrees,
                    CoveragePaddingPixels
                );

                // We keep the scanner at its fitted screen position while changing scale.
                return fitted with { CmToPixels = fitted.CmToPixels * _zoom };
            }

            var originScreenX = left + width / 2;
            var originScreenY = top + height / 2;

            var desiredVisibleHeightCm = ScannerDefinition?.RangeSensor.MaxRangeCm * 1.25 ?? DefaultScanHeightCm;
            var cmToPixels = height / desiredVisibleHeightCm * _zoom;

            return new(left, top, width, height, originScreenX, originScreenY, cmToPixels);
        }

        #endregion

        #region Cartesian Grid and Rulers

        private double GetMajorGridSpacingCm()
        {
            var visibleRangeCm = (ScannerDefinition?.RangeSensor.MaxRangeCm ?? DefaultScanHeightCm) / _zoom;

            return visibleRangeCm switch
            {
                <= 25 => GridSpacingVeryFineCm,
                <= 50 => GridSpacingExtraFineCm,
                <= 100 => GridSpacingCloseCm,
                <= 200 => GridSpacingFineCm,
                <= 400 => GridSpacingMediumCm,
                _ => GridSpacingCoarseCm
            };
        }

        private CartesianGridLayout CalculateCartesianGridLayout(ViewportTransform transform)
        {
            var minWorldX = transform.ScreenToWorldX(transform.Left);
            var maxWorldX = transform.ScreenToWorldX(transform.Left + transform.Width);

            var maxWorldY = transform.ScreenToWorldY(transform.Top);
            var minWorldY = transform.ScreenToWorldY(transform.Top + transform.Height);

            var majorGridSpacingCm = GetMajorGridSpacingCm();

            var firstGridX = Math.Ceiling(minWorldX / majorGridSpacingCm) * majorGridSpacingCm;
            var lastGridX = Math.Floor(maxWorldX / majorGridSpacingCm) * majorGridSpacingCm;

            var firstGridY = Math.Ceiling(minWorldY / majorGridSpacingCm) * majorGridSpacingCm;
            var lastGridY = Math.Floor(maxWorldY / majorGridSpacingCm) * majorGridSpacingCm;

            var horizontalCount = (int)((lastGridY - firstGridY) / majorGridSpacingCm) + 1;
            var verticalCount = (int)((lastGridX - firstGridX) / majorGridSpacingCm) + 1;

            return new(firstGridX, firstGridY, verticalCount, horizontalCount);
        }

        private Line CreateRulerTick() => new()
        {
            Stroke = _scanRulerLineBrush,
            StrokeThickness = StrokeThicknessSmall
        };

        private void EnsureCartesianGridElements(CartesianGridLayout layout)
        {
            EnsureElementCount(_horizontalCartesianGridLines, layout.HorizontalCount, CartesianGridLayer, CreateGridLine);
            EnsureElementCount(_verticalCartesianGridLines, layout.VerticalCount, CartesianGridLayer, CreateGridLine);
            EnsureElementCount(
                _horizontalRulerLabels,
                layout.HorizontalCount,
                RulerLayer,
                () => new() { Foreground = _scanAnnotationBrush, LayoutTransform = new RotateTransform(270) }
            );
            EnsureElementCount(_verticalRulerLabels, layout.VerticalCount, RulerLayer, CreateRulerLabel);
            EnsureElementCount(_horizontalMajorTicks, layout.HorizontalCount, RulerLayer, CreateRulerTick);
            EnsureElementCount(_verticalMajorTicks, layout.VerticalCount, RulerLayer, CreateRulerTick);
            EnsureElementCount(_horizontalMinorTicks, Math.Max(0, layout.HorizontalCount - 1), RulerLayer, CreateRulerTick);
            EnsureElementCount(_verticalMinorTicks, Math.Max(0, layout.VerticalCount - 1), RulerLayer, CreateRulerTick);
        }

        private void LayoutCartesianGridLines(ViewportTransform transform, CartesianGridLayout layout)
        {
            var majorGridSpacingCm = GetMajorGridSpacingCm();

            for (var i = 0; i < layout.VerticalCount; i++)
            {
                var worldX = layout.FirstX + i * majorGridSpacingCm;
                var screenX = transform.WorldToScreenX(worldX);

                var line = _verticalCartesianGridLines[i];

                line.X1 = screenX;
                line.Y1 = transform.Top;
                line.X2 = screenX;
                line.Y2 = transform.Top + transform.Height;
            }

            for (var j = 0; j < layout.HorizontalCount; j++)
            {
                var worldY = layout.FirstY + j * majorGridSpacingCm;
                var screenY = transform.WorldToScreenY(worldY);

                var line = _horizontalCartesianGridLines[j];

                line.X1 = transform.Left;
                line.Y1 = screenY;
                line.X2 = transform.Width + transform.Left;
                line.Y2 = screenY;
            }
        }

        private void LayoutRulers(ViewportTransform transform, CartesianGridLayout layout)
        {
            _canvasBorderRectangle.Width = transform.Width + transform.Left;
            _canvasBorderRectangle.Height = transform.Height + transform.Top;

            Canvas.SetLeft(_canvasBorderRectangle, 0);
            Canvas.SetTop(_canvasBorderRectangle, 0);

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

            var majorGridSpacingCm = GetMajorGridSpacingCm();
            var previousLabelRight = double.NegativeInfinity;

            for (var i = 0; i < layout.VerticalCount; i++)
            {
                var worldX = layout.FirstX + i * majorGridSpacingCm;
                var screenX = transform.WorldToScreenX(worldX);

                var label = _verticalRulerLabels[i];
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

                var labelFits = markFitsHorizontally && labelLeft >= previousLabelRight + RulerLabelGapPixels;
                label.Visibility = labelFits ? Visibility.Visible : Visibility.Collapsed;
                if (labelFits)
                    previousLabelRight = labelRight;
                tick.Visibility = markFitsHorizontally ? Visibility.Visible : Visibility.Collapsed;
            }

            // World Y increases upward, so these labels are visited bottom to top.
            var previousLabelTop = double.PositiveInfinity;
            for (var j = 0; j < layout.HorizontalCount; j++)
            {
                var worldY = layout.FirstY + j * majorGridSpacingCm;
                var screenY = transform.WorldToScreenY(worldY);

                var label = _horizontalRulerLabels[j];
                label.Text = $"{worldY} cm";

                var (labelWidth, _) = GetTextBlockSize(label);
                var labelTop = screenY - labelWidth / 2;
                var labelBottom = screenY + labelWidth / 2;

                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, labelTop);

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

                var labelFits = markFitsVertically && labelBottom <= previousLabelTop - RulerLabelGapPixels;
                label.Visibility = labelFits ? Visibility.Visible : Visibility.Collapsed;
                if (labelFits)
                    previousLabelTop = labelTop;
                tick.Visibility = markFitsVertically ? Visibility.Visible : Visibility.Collapsed;
            }

            var minorRulerTickOffsetCm = majorGridSpacingCm / 2;

            var verticalMinorTickCount = Math.Max(0, layout.VerticalCount - 1);
            for (var i = 0; i < verticalMinorTickCount; i++)
            {
                var worldX = layout.FirstX + i * majorGridSpacingCm + minorRulerTickOffsetCm;
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
                var worldY = layout.FirstY + j * majorGridSpacingCm + minorRulerTickOffsetCm;
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

        #endregion

        #region Polar Grid and Labels

        private IEnumerable<double> GetPolarGridBearings()
        {
            if (ScannerConfiguration is not { } configuration)
            {
                yield break;
            }

            var firstBearing = Math.Ceiling(configuration.MinBearingDegrees / PolarBearingStepDegrees) * PolarBearingStepDegrees;
            for (var bearing = firstBearing; bearing <= configuration.MaxBearingDegrees; bearing += PolarBearingStepDegrees)
            {
                yield return bearing;
            }
        }

        private void LayoutPolarBearingLabel(
            ViewportTransform transform,
            TextBlock label,
            double bearingDegrees,
            double rangeCm
        )
        {
            var endpoint = GetScreenPointAtBearing(
                transform,
                bearingDegrees,
                rangeCm
            );

            var directionX = endpoint.X - transform.OriginScreenX;
            var directionY = endpoint.Y - transform.OriginScreenY;

            var length = Math.Sqrt(directionX * directionX + directionY * directionY);

            if (length <= 0)
            {
                return;
            }

            directionX /= length;
            directionY /= length;

            var labelX = endpoint.X + directionX * PolarLabelOffsetPixels;
            var labelY = endpoint.Y + directionY * PolarLabelOffsetPixels;

            var (labelWidth, labelHeight) = GetTextBlockSize(label);

            Canvas.SetLeft(label, labelX - labelWidth / 2);
            Canvas.SetTop(label, labelY - labelHeight / 2);
        }

        private void LayoutPolarGrid(ViewportTransform transform)
        {
            var bearings = ScannerDefinition is not null ? GetPolarGridBearings().ToArray() : [];
            EnsureElementCount(_polarBearingLines, bearings.Length, PolarGridLayer, CreateGridLine);
            EnsureElementCount(_polarBearingLabels, bearings.Length, AnnotationLayer, CreateRulerLabel);
            if (ScannerDefinition is not { } definition)
            {
                return;
            }

            var rangeCm = definition.RangeSensor.MaxRangeCm / _zoom;

            for (var i = 0; i < bearings.Length; i++)
            {
                var bearing = bearings[i];
                _polarBearingLabels[i].Text = $"{bearing:0}°";

                var endpoint = GetScreenPointAtBearing(
                    transform,
                    bearing,
                    rangeCm
                );

                var line = _polarBearingLines[i];

                line.X1 = transform.OriginScreenX;
                line.Y1 = transform.OriginScreenY;
                line.X2 = endpoint.X;
                line.Y2 = endpoint.Y;

                LayoutPolarBearingLabel(
                    transform,
                    _polarBearingLabels[i],
                    bearing,
                    rangeCm
                );
            }
        }

        #endregion

        #region Coverage and Scanner Marker

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
            if (ScannerDefinition is not { } definition || ScannerConfiguration is not { } configuration)
            {
                _layoutScannerCoveragePath.Visibility = _coverageFillPath.Visibility = Visibility.Collapsed;

                return;
            }

            _layoutScannerCoveragePath.Visibility = _coverageFillPath.Visibility = Visibility.Visible;
            var minRangeCm = definition.RangeSensor.MinRangeCm;
            var maxRangeCm = definition.RangeSensor.MaxRangeCm;
            var minBearingDegrees = configuration.MinBearingDegrees;
            var maxBearingDegrees = configuration.MaxBearingDegrees;

            var minRadiusPixels = minRangeCm * transform.CmToPixels;
            var maxRadiusPixels = maxRangeCm * transform.CmToPixels;

            var minBearingInnerPoint = GetScreenPointAtBearing(transform, minBearingDegrees, minRangeCm);
            var minBearingOuterPoint = GetScreenPointAtBearing(transform, minBearingDegrees, maxRangeCm);

            var maxBearingOuterPoint = GetScreenPointAtBearing(transform, maxBearingDegrees, maxRangeCm);
            var maxBearingInnerPoint = GetScreenPointAtBearing(transform, maxBearingDegrees, minRangeCm);

            var sweepAngleDegrees = maxBearingDegrees - minBearingDegrees;
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

        #endregion

        #region Observations and Latest Measurement

        private void UpdateLatestObservationIndicator(ViewportTransform transform)
        {
            if (Session?.LatestObservation is { PositionXCm: { } x, PositionYCm: { } y } observation)
            {
                ShowLatestObservationIndicator(transform, observation, x, y);
            }
            else
            {
                HideLatestObservationIndicator();
            }
        }

        private void ShowLatestObservationIndicator(
            ViewportTransform transform,
            RangeObservation observation,
            double positionXCm,
            double positionYCm
        )
        {
            _observationLine.Visibility = Visibility.Visible;
            _observationEndpointEllipse.Visibility = Visibility.Visible;

            var x = transform.WorldToScreenX(positionXCm);
            var y = transform.WorldToScreenY(positionYCm);

            _observationLine.X1 = transform.OriginScreenX;
            _observationLine.Y1 = transform.OriginScreenY;
            _observationLine.X2 = x;
            _observationLine.Y2 = y;

            var stroke = GetObservationBrush(observation);

            _observationLine.Stroke = stroke;

            _observationEndpointEllipse.Width = LatestObservationMarkerRadiusPixels * 2;
            _observationEndpointEllipse.Height = LatestObservationMarkerRadiusPixels * 2;
            _observationEndpointEllipse.Fill = stroke;

            Canvas.SetLeft(_observationEndpointEllipse, _observationLine.X2 - LatestObservationMarkerRadiusPixels);
            Canvas.SetTop(_observationEndpointEllipse, _observationLine.Y2 - LatestObservationMarkerRadiusPixels);
        }

        private void HideLatestObservationIndicator()
        {
            _observationLine.Visibility = Visibility.Collapsed;
            _observationEndpointEllipse.Visibility = Visibility.Collapsed;
        }

        private Ellipse CreateObservationMarker() => new()
        {
            Width = ObservationMarkerRadiusPixels * 2,
            Height = ObservationMarkerRadiusPixels * 2
        };

        private Brush GetObservationBrush(RangeObservation observation) =>
            observation.RangeStatus == RangeStatus.InRange
                ? _scanObservationBrush
                : _scanObservationErrorBrush;

        private void LayoutObservationMarker(Ellipse marker, ViewportTransform transform, RangeObservation observation)
        {
            if (observation.PositionXCm is not { } positionX || observation.PositionYCm is not { } positionY)
            {
                marker.Visibility = Visibility.Collapsed;
                return;
            }

            marker.Visibility = Visibility.Visible;
            marker.Fill = GetObservationBrush(observation);

            Canvas.SetLeft(marker, transform.WorldToScreenX(positionX) - ObservationMarkerRadiusPixels);
            Canvas.SetTop(marker, transform.WorldToScreenY(positionY) - ObservationMarkerRadiusPixels);
        }

        private void AddObservationPoint(ViewportTransform transform, RangeObservation observation)
        {
            var marker = CreateObservationMarker();

            _observationMarkers.Add(marker);
            ObservationLayer.Children.Add(marker);

            LayoutObservationMarker(marker, transform, observation);
        }

        private void LayoutObservations(ViewportTransform transform)
        {
            var observations = Session is { } session
                ? session.Observations.Where(observation => observation.DistanceCm.HasValue).ToArray()
                : [];
            var count = observations.Length;

            EnsureElementCount(_observationMarkers, count, ObservationLayer, CreateObservationMarker);

            for (var i = 0; i < count; i++)
            {
                LayoutObservationMarker(_observationMarkers[i], transform, observations[i]);
            }
        }

        #endregion

        #region Shared Drawing Helpers

        private static void EnsureElementCount<T>(List<T> elements, int count, Canvas layer, Func<T> create) where T : UIElement
        {
            while (elements.Count > count)
            {
                layer.Children.Remove(elements[^1]);
                elements.RemoveAt(elements.Count - 1);
            }

            while (elements.Count < count)
            {
                var element = create();
                elements.Add(element);
                layer.Children.Add(element);
            }
        }

        private Line CreateGridLine() => new()
        {
            Stroke = _scanGridLineBrush,
            StrokeThickness = StrokeThicknessSmall
        };

        private TextBlock CreateRulerLabel() => new() { Foreground = _scanAnnotationBrush };

        private (double Width, double Height) GetTextBlockSize(TextBlock textBlock)
        {
            // https://learn.microsoft.com/en-us/answers/questions/400490/how-to-get-the-actual-width-of-textblock
            var formatted = new FormattedText(
                textBlock.Text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize,
                textBlock.Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            );

            return (formatted.Width, formatted.Height);
        }

        #endregion

    }
}
