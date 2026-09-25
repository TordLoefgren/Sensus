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
    /// Interaction logic for ViewportView.xaml
    /// </summary>
    public partial class ViewportView : UserControl
    {
        #region Viewport Geometry

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

        #endregion

        #region Rendering Brushes

        private readonly Brush _viewportGridLineBrush;
        private readonly Brush _viewportRulerBackgroundBrush;
        private readonly Brush _viewportRulerLineBrush;
        private readonly Brush _viewportRulerTextBrush;
        private readonly Brush _viewportObservationBrush;
        private readonly Brush _viewportObservationErrorBrush;
        private readonly Brush _viewportRangeStrokeBrush;
        private readonly Brush _viewportRangeFillBrush;

        #endregion

        #region Grid and Ruler Elements

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

        #endregion

        #region Scanner and Observation Elements

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

        #region View State

        public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
            nameof(Session),
            typeof(ScannerSession),
            typeof(ViewportView),
            new FrameworkPropertyMetadata(null, OnSessionChanged)
        );

        public static readonly DependencyProperty ScannerDefinitionProperty = DependencyProperty.Register(
            nameof(ScannerDefinition),
            typeof(ScannerDefinition?),
            typeof(ViewportView),
            new FrameworkPropertyMetadata(null, OnScannerGeometryChanged)
        );

        public static readonly DependencyProperty ScannerConfigurationProperty = DependencyProperty.Register(
            nameof(ScannerConfiguration),
            typeof(ScannerConfiguration?),
            typeof(ViewportView),
            new FrameworkPropertyMetadata(null, OnScannerGeometryChanged)
        );

        public static readonly DependencyProperty MousePositionCmProperty = DependencyProperty.Register(
            nameof(MousePositionCm),
            typeof(Point?),
            typeof(ViewportView),
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

        #endregion

        public ViewportView()
        {
            InitializeComponent();

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

            InitializeGizmoLayer();

            Loaded += ViewportView_Loaded;
            Unloaded += ViewportView_Unloaded;

            ViewportHost.SizeChanged += ViewportHost_SizeChanged;
            ViewportHost.MouseMove += ViewportHost_MouseMove;
            ViewportHost.MouseLeave += ViewportHost_MouseLeave;
        }

        #region Session and View Events

        private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (ViewportView)d;
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

            view.LayoutViewportForCurrentSize();
        }

        private static void OnScannerGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (ViewportView)d;
            if (view._isRendering)
            {
                view.LayoutViewportForCurrentSize();
            }
        }

        private void ViewportView_Loaded(object sender, RoutedEventArgs e)
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

            LayoutViewportForCurrentSize();
        }

        private void ViewportView_Unloaded(object sender, RoutedEventArgs e)
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
            var transform = GetViewportTransform();
            if (transform.Width <= 0 || transform.Height <= 0)
            {
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } newItems)
            {
                foreach (RangeObservation observation in newItems)
                {
                    AddObservationPoint(transform, observation);
                }
            }
            else
            {
                PopulateObservationLayer(transform);
            }

            if (Session?.LatestObservation is { } latest)
            {
                ShowLatestObservationIndicator(transform, latest);
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

        #endregion

        #region Mouse Input

        private void ViewportHost_MouseMove(object sender, MouseEventArgs e) => UpdateMousePosition(e.GetPosition(ViewportHost));

        private void ViewportHost_MouseLeave(object sender, MouseEventArgs e) => SetCurrentValue(MousePositionCmProperty, null);

        private void UpdateMousePosition(Point position)
        {
            var transform = GetViewportTransform();
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

        #region Viewport Layout

        private void LayoutViewportForCurrentSize()
        {
            if (!_isRendering)
            {
                return;
            }

            var transform = GetViewportTransform();
            if (transform.Width <= 0 || transform.Height <= 0)
            {
                SetCurrentValue(MousePositionCmProperty, null);
                return;
            }
            var gridLayout = CalculateGridLayout(transform);

            PopulateGridLayer(gridLayout);

            LayoutGridLines(transform, gridLayout);
            LayoutRulers(transform, gridLayout);

            LayoutScannerCoverage(transform);
            LayoutScannerMarker(transform);

            PopulateObservationLayer(transform);

            if (Session?.LatestObservation is { } observation)
            {
                ShowLatestObservationIndicator(transform, observation);
            }
            else
            {
                HideLatestObservationIndicator();
            }

            if (ViewportHost.IsMouseOver)
            {
                UpdateMousePosition(Mouse.GetPosition(ViewportHost));
            }
            else
            {
                SetCurrentValue(MousePositionCmProperty, null);
            }
        }

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

        #endregion

        #region Grid and Ruler Layout

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

        #endregion

        #region Scanner Geometry

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
            if (ScannerDefinition is not { } definition || ScannerConfiguration is not { } configuration)
            {
                _layoutScannerCoveragePath.Visibility = Visibility.Collapsed;

                return;
            }

            _layoutScannerCoveragePath.Visibility = Visibility.Visible;
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

        #region Observation Rendering

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

        }

        private void PopulateObservationLayer(ViewportTransform transform)
        {
            ObjectLayer.Children.Clear();

            if (Session is not { } session)
            {
                return;
            }

            foreach (var observation in session.Observations)
            {
                AddObservationPoint(transform, observation);
            }
        }

        #endregion
    }
}
