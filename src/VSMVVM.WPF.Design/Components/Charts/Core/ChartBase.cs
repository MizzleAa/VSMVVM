using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public abstract class ChartBase : FrameworkElement, IChartViewport, IFittable
    {
        #region Dependency Properties

        public static readonly DependencyProperty SeriesProperty =
            DependencyProperty.Register(nameof(Series), typeof(IList<ChartSeries>), typeof(ChartBase),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsRender, OnSeriesChanged));

        public static readonly DependencyProperty XAxisProperty =
            DependencyProperty.Register(nameof(XAxis), typeof(ChartAxis), typeof(ChartBase),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsRender, OnAxisChanged));

        public static readonly DependencyProperty YAxisProperty =
            DependencyProperty.Register(nameof(YAxis), typeof(ChartAxis), typeof(ChartBase),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsRender, OnAxisChanged));

        public static readonly DependencyProperty PlotMarginProperty =
            DependencyProperty.Register(nameof(PlotMargin), typeof(Thickness), typeof(ChartBase),
                new FrameworkPropertyMetadata(new Thickness(56, 12, 16, 44),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PlotBackgroundBrushProperty =
            DependencyProperty.Register(nameof(PlotBackgroundBrush), typeof(Brush), typeof(ChartBase),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GridBrushProperty =
            DependencyProperty.Register(nameof(GridBrush), typeof(Brush), typeof(ChartBase),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AxisBrushProperty =
            DependencyProperty.Register(nameof(AxisBrush), typeof(Brush), typeof(ChartBase),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TextBrushProperty =
            DependencyProperty.Register(nameof(TextBrush), typeof(Brush), typeof(ChartBase),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsZoomEnabledProperty =
            DependencyProperty.Register(nameof(IsZoomEnabled), typeof(bool), typeof(ChartBase),
                new PropertyMetadata(true));

        public static readonly DependencyProperty IsPanEnabledProperty =
            DependencyProperty.Register(nameof(IsPanEnabled), typeof(bool), typeof(ChartBase),
                new PropertyMetadata(true));

        public static readonly DependencyProperty MinZoomProperty =
            DependencyProperty.Register(nameof(MinZoom), typeof(double), typeof(ChartBase),
                new PropertyMetadata(0.1));

        public static readonly DependencyProperty MaxZoomProperty =
            DependencyProperty.Register(nameof(MaxZoom), typeof(double), typeof(ChartBase),
                new PropertyMetadata(1000.0));

        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(ChartBase),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty IsTooltipEnabledProperty =
            DependencyProperty.Register(nameof(IsTooltipEnabled), typeof(bool), typeof(ChartBase),
                new PropertyMetadata(true));

        public static readonly DependencyProperty TooltipAnchorProperty =
            DependencyProperty.Register(nameof(TooltipAnchor), typeof(TooltipAnchor), typeof(ChartBase),
                new PropertyMetadata(Charts.Core.TooltipAnchor.FollowMouse));

        public static readonly DependencyProperty HoverModeProperty =
            DependencyProperty.Register(nameof(HoverMode), typeof(ChartHoverMode), typeof(ChartBase),
                new PropertyMetadata(ChartHoverMode.NearestPoint));

        /// <summary>
        /// true 면 categorical 축(X 또는 Y)의 라벨 실측 폭/높이를 기반으로 <see cref="PlotRect"/> 의 좌/하단 여백을
        /// 자동 확장한다. 긴 카테고리명("Anomaly Detection" 등) 이 잘리는 현상을 방지.
        /// <see cref="PlotMargin"/> 은 최소 보장값으로 동작하며, 라벨이 더 길면 그만큼 더 키운다.
        /// </summary>
        public static readonly DependencyProperty AutoFitAxisLabelsProperty =
            DependencyProperty.Register(nameof(AutoFitAxisLabels), typeof(bool), typeof(ChartBase),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// true 면 차트가 처음 화면에 로드될 때 단 한 번 진입 애니메이션을 재생한다 (BarChart 막대가 자라남, LineChart 좌→우 그려짐 등).
        /// 데이터 갱신/Series 교체 시에는 재생하지 않음 — 사용자 거슬림 방지.
        /// 자식 차트의 DrawPlot 가 <see cref="AnimationProgress"/> 를 곱해 부분 렌더링한다.
        /// </summary>
        public static readonly DependencyProperty IsAnimationEnabledProperty =
            DependencyProperty.Register(nameof(IsAnimationEnabled), typeof(bool), typeof(ChartBase),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, OnAnimationEnabledChanged));

        public static readonly DependencyProperty AnimationDurationProperty =
            DependencyProperty.Register(nameof(AnimationDuration), typeof(TimeSpan), typeof(ChartBase),
                new PropertyMetadata(TimeSpan.FromMilliseconds(600)));

        /// <summary>
        /// 이 값이 바뀌면(아무 값이어도 OK, new object() 등) <see cref="RestartAnimation"/> 이 호출돼 진입 애니메이션이 재생된다.
        /// ViewModel 에서 코드비하인드 없이 차트 애니메이션을 트리거하기 위한 hook.
        /// </summary>
        public static readonly DependencyProperty AnimationRestartTriggerProperty =
            DependencyProperty.Register(nameof(AnimationRestartTrigger), typeof(object), typeof(ChartBase),
                new PropertyMetadata(null, OnAnimationRestartTriggerChanged));

        public IList<ChartSeries> Series { get => (IList<ChartSeries>)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
        public ChartAxis XAxis { get => (ChartAxis)GetValue(XAxisProperty); set => SetValue(XAxisProperty, value); }
        public ChartAxis YAxis { get => (ChartAxis)GetValue(YAxisProperty); set => SetValue(YAxisProperty, value); }
        public Thickness PlotMargin { get => (Thickness)GetValue(PlotMarginProperty); set => SetValue(PlotMarginProperty, value); }
        public bool AutoFitAxisLabels { get => (bool)GetValue(AutoFitAxisLabelsProperty); set => SetValue(AutoFitAxisLabelsProperty, value); }
        public bool IsAnimationEnabled { get => (bool)GetValue(IsAnimationEnabledProperty); set => SetValue(IsAnimationEnabledProperty, value); }
        public TimeSpan AnimationDuration { get => (TimeSpan)GetValue(AnimationDurationProperty); set => SetValue(AnimationDurationProperty, value); }
        public object AnimationRestartTrigger { get => GetValue(AnimationRestartTriggerProperty); set => SetValue(AnimationRestartTriggerProperty, value); }

        /// <summary>
        /// 진입 애니메이션 진행률 (0.0 → 1.0). 자식 차트의 DrawPlot 에서 곱해 부분 렌더링.
        /// 비활성 / 완료 / 한 번 재생 끝난 후 영구히 1.0.
        /// </summary>
        protected double AnimationProgress { get; private set; } = 1.0;
        public Brush PlotBackgroundBrush { get => (Brush)GetValue(PlotBackgroundBrushProperty); set => SetValue(PlotBackgroundBrushProperty, value); }
        public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
        public Brush AxisBrush { get => (Brush)GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
        public Brush TextBrush { get => (Brush)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
        public bool IsZoomEnabled { get => (bool)GetValue(IsZoomEnabledProperty); set => SetValue(IsZoomEnabledProperty, value); }
        public bool IsPanEnabled { get => (bool)GetValue(IsPanEnabledProperty); set => SetValue(IsPanEnabledProperty, value); }
        public double MinZoom { get => (double)GetValue(MinZoomProperty); set => SetValue(MinZoomProperty, value); }
        public double MaxZoom { get => (double)GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }
        public double ZoomLevel { get => (double)GetValue(ZoomLevelProperty); set => SetValue(ZoomLevelProperty, value); }
        public bool IsTooltipEnabled { get => (bool)GetValue(IsTooltipEnabledProperty); set => SetValue(IsTooltipEnabledProperty, value); }
        public TooltipAnchor TooltipAnchor { get => (TooltipAnchor)GetValue(TooltipAnchorProperty); set => SetValue(TooltipAnchorProperty, value); }
        public ChartHoverMode HoverMode { get => (ChartHoverMode)GetValue(HoverModeProperty); set => SetValue(HoverModeProperty, value); }

        public Point ResolveTooltipPosition(Point hoverScreen, Size tooltipSize, double margin = 12)
        {
            var anchor = TooltipAnchor;
            var w = tooltipSize.Width;
            var h = tooltipSize.Height;
            double left, top;
            switch (anchor)
            {
                case TooltipAnchor.TopLeft:
                    left = margin; top = margin; break;
                case TooltipAnchor.TopRight:
                    left = ActualWidth - w - margin; top = margin; break;
                case TooltipAnchor.BottomLeft:
                    left = margin; top = ActualHeight - h - margin; break;
                case TooltipAnchor.BottomRight:
                    left = ActualWidth - w - margin; top = ActualHeight - h - margin; break;
                default:
                    left = hoverScreen.X + margin; top = hoverScreen.Y + margin;
                    if (left + w > ActualWidth) left = hoverScreen.X - w - margin;
                    if (top + h > ActualHeight) top = hoverScreen.Y - h - margin;
                    break;
            }
            if (left < 0) left = 0;
            if (top < 0) top = 0;
            return new Point(left, top);
        }

        #endregion

        #region Viewport state

        protected double DataMinX = 0, DataMaxX = 1, DataMinY = 0, DataMaxY = 1;
        protected double ViewMinX = 0, ViewMaxX = 1, ViewMinY = 0, ViewMaxY = 1;
        protected bool DataRangeDirty = true;

        protected readonly FormattedTextCache TextCache = new();
        private readonly Dictionary<(Brush brush, double thickness), Pen> _penCache = new();
        private readonly Dictionary<Brush, Brush> _frozenBrushCache = new();

        private bool _isPanning;
        private Point _panStartPx;
        private double _panStartViewMinX, _panStartViewMaxX, _panStartViewMinY, _panStartViewMaxY;

        public event EventHandler<ChartHoverState> HoverChanged;
        public event EventHandler<XUnifiedHoverState> XUnifiedHoverChanged;
        public event EventHandler<ViewportChangedEventArgs> ViewportChangedEx;
        private event EventHandler _viewportChanged;
        public event SizeChangedEventHandler SizeChangedEx;

        #endregion

        protected ChartBase()
        {
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
            Loaded += OnLoadedSetDpi;
            Loaded += OnLoadedPlayIntro;
            Unloaded += OnUnloadedStopAnimation;
            SizeChanged += (s, e) => SizeChangedEx?.Invoke(s, e);
        }

        private void OnLoadedSetDpi(object sender, RoutedEventArgs e)
        {
            try { TextCache.SetPixelsPerDip(VisualTreeHelper.GetDpi(this).PixelsPerDip); }
            catch { TextCache.SetPixelsPerDip(1.0); }
        }

        #region Intro animation (Loaded 시 한 번)

        // hide/show 반복 / virtualization 으로 Loaded 가 여러 번 발화돼도 단 한 번만 재생.
        private bool _hasPlayedIntro;
        private bool _isAnimating;
        private DateTime _animationStartUtc;

        public void RestartAnimation()
        {
            _hasPlayedIntro = false;
            StartIntroAnimation();
        }

        private void OnLoadedPlayIntro(object sender, RoutedEventArgs e)
        {
            if (_hasPlayedIntro) return;
            // 데이터가 아직 비어있으면 재생을 미루고 대기. 첫 데이터 도착 시 OnSeriesInvalidated 에서 재생.
            if (!HasAnyVisibleData()) return;
            _hasPlayedIntro = true;
            StartIntroAnimation();
        }

        /// <summary>
        /// Series 변경/컬렉션 갱신 hook 에서 호출. Loaded 이후 첫 데이터가 도착한 시점이면 그때 한 번 재생.
        /// </summary>
        private void TryPlayIntroAfterDataArrived()
        {
            if (_hasPlayedIntro) return;
            if (!IsLoaded) return;            // Loaded 전이면 그쪽 핸들러가 처리.
            if (!HasAnyVisibleData()) return; // 여전히 비어있으면 더 기다림.
            _hasPlayedIntro = true;
            StartIntroAnimation();
        }

        /// <summary>가시 시리즈 중 하나라도 데이터(YValues 비어있지 않음) 가 있는지.</summary>
        private bool HasAnyVisibleData()
        {
            var series = Series;
            if (series == null) return false;
            foreach (var s in series)
            {
                if (s == null || !s.IsVisible) continue;
                s.GetArrays(out var _, out var ys);
                if (ys != null && ys.Length > 0) return true;
            }
            return false;
        }

        private void StartIntroAnimation()
        {
            if (!IsAnimationEnabled || AnimationDuration <= TimeSpan.Zero)
            {
                AnimationProgress = 1.0;
                InvalidateVisual();
                return;
            }
            AnimationProgress = 0.0;
            _animationStartUtc = DateTime.UtcNow;
            if (!_isAnimating)
            {
                _isAnimating = true;
                CompositionTarget.Rendering += OnAnimationFrame;
            }
            InvalidateVisual();
        }

        private void OnAnimationFrame(object sender, EventArgs e)
        {
            var elapsed = DateTime.UtcNow - _animationStartUtc;
            var dur = AnimationDuration.TotalMilliseconds;
            var t = dur <= 0 ? 1.0 : Math.Min(1.0, elapsed.TotalMilliseconds / dur);
            // ease-out cubic — 끝부분 부드러운 감속.
            AnimationProgress = 1 - Math.Pow(1 - t, 3);
            InvalidateVisual();
            if (t >= 1.0)
            {
                _isAnimating = false;
                CompositionTarget.Rendering -= OnAnimationFrame;
            }
        }

        private void OnUnloadedStopAnimation(object sender, RoutedEventArgs e)
        {
            if (_isAnimating)
            {
                CompositionTarget.Rendering -= OnAnimationFrame;
                _isAnimating = false;
            }
        }

        private static void OnAnimationEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChartBase c && (bool)e.NewValue == false)
            {
                // 비활성으로 바뀌면 진행 중이던 애니메이션 즉시 종료.
                c.AnimationProgress = 1.0;
                if (c._isAnimating)
                {
                    CompositionTarget.Rendering -= c.OnAnimationFrame;
                    c._isAnimating = false;
                }
                c.InvalidateVisual();
            }
        }

        private static void OnAnimationRestartTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChartBase c) c.RestartAnimation();
        }

        #endregion

        #region Series / Axis change

        private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChartBase cb)
            {
                if (e.OldValue is INotifyCollectionChanged oldCol) oldCol.CollectionChanged -= cb.OnSeriesCollectionChanged;
                if (e.OldValue is IEnumerable<ChartSeries> oldList) cb.UnsubscribeSeries(oldList);
                if (e.NewValue is INotifyCollectionChanged newCol) newCol.CollectionChanged += cb.OnSeriesCollectionChanged;
                if (e.NewValue is IEnumerable<ChartSeries> newList) cb.SubscribeSeries(newList);
                cb.AssignDefaultSeriesBrushes();
                cb.DataRangeDirty = true;
                cb.OnSeriesInvalidated();
                cb.TryPlayIntroAfterDataArrived();
                cb.InvalidateVisual();
            }
        }

        private void OnSeriesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null) UnsubscribeSeries(e.OldItems);
            if (e.NewItems != null) SubscribeSeries(e.NewItems);
            AssignDefaultSeriesBrushes();
            DataRangeDirty = true;
            OnSeriesInvalidated();
            TryPlayIntroAfterDataArrived();
            InvalidateVisual();
        }

        private void SubscribeSeries(System.Collections.IEnumerable list)
        {
            if (list == null) return;
            foreach (var item in list)
            {
                if (item is ChartSeries s)
                {
                    s.DataChanged += OnSeriesItemDataChanged;
                    s.VisualChanged += OnSeriesItemVisualChanged;
                }
            }
        }

        private void UnsubscribeSeries(System.Collections.IEnumerable list)
        {
            if (list == null) return;
            foreach (var item in list)
            {
                if (item is ChartSeries s)
                {
                    s.DataChanged -= OnSeriesItemDataChanged;
                    s.VisualChanged -= OnSeriesItemVisualChanged;
                }
            }
        }

        private void OnSeriesItemDataChanged(object sender, EventArgs e)
        {
            DataRangeDirty = true;
            OnSeriesInvalidated();
            TryPlayIntroAfterDataArrived();
            InvalidateVisual();
        }

        // ChartSeries.OnAnyChanged 는 IsVisible / Title / Brush / StrokeThickness / MarkerSize / MarkerShape 변경을 전부
        // VisualChanged 로 통지한다. 이 중 IsVisible 만 ComputeDataRange / Histogram.EnsureBinned 의 visible-only 산출
        // 결과에 영향을 미치므로, DataRangeDirty 도 함께 set 해서 다음 OnRender 에서 범위/binning 이 새 visibility
        // 기준으로 재계산되도록 한다. (Brush 등 다른 visual 변경은 한 번 더 ComputeDataRange 가 같은 값을 산출해 무해함.)
        // OnSeriesInvalidated hook 으로 파생 차트(Histogram 등)가 자체 binning 캐시를 무효화할 수 있도록 한다.
        private void OnSeriesItemVisualChanged(object sender, EventArgs e)
        {
            DataRangeDirty = true;
            OnSeriesInvalidated();
            InvalidateVisual();
        }

        /// <summary>Series 컬렉션 / 항목의 데이터·시각 상태가 변경되었을 때 호출되는 hook.
        /// 파생 차트가 자체 캐시(예: Histogram 의 binning 결과) 를 무효화하기 위해 override.
        /// 기본 구현은 no-op. InvalidateVisual 은 caller 가 이미 호출하므로 여기서 다시 부르지 말 것.</summary>
        protected virtual void OnSeriesInvalidated() { }

        private static void OnAxisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChartBase cb)
            {
                if (e.OldValue is ChartAxis oldA) oldA.Changed -= cb.OnAxisItemChanged;
                if (e.NewValue is ChartAxis newA) newA.Changed += cb.OnAxisItemChanged;
                cb.DataRangeDirty = true;
                cb.InvalidateVisual();
            }
        }

        private void OnAxisItemChanged(object sender, EventArgs e)
        {
            DataRangeDirty = true;
            InvalidateVisual();
        }

        protected void AssignDefaultSeriesBrushes()
        {
            var series = Series;
            if (series == null) return;
            var palette = ChartPalette.GetPalette(this);
            if (palette == null || palette.Count == 0) return;
            var i = 0;
            foreach (var s in series)
            {
                if (s != null && s.Brush == null)
                {
                    s.Brush = palette[i % palette.Count];
                }
                i++;
            }
        }

        #endregion

        #region Coordinate helpers

        public Rect PlotRect
        {
            get
            {
                var m = PlotMargin;
                if (AutoFitAxisLabels)
                {
                    var (extraLeft, extraBottom) = MeasureCategoricalAxisOverhead();
                    if (extraLeft > m.Left) m = new Thickness(extraLeft, m.Top, m.Right, m.Bottom);
                    if (extraBottom > m.Bottom) m = new Thickness(m.Left, m.Top, m.Right, extraBottom);

                    // 자식 차트(BarChart 등) 가 그리는 값 라벨이 plot 영역 우/상단을 넘는 경우 자동 확보.
                    var (extraRight, extraTop) = MeasureValueLabelOverhead();
                    if (extraRight > m.Right) m = new Thickness(m.Left, m.Top, extraRight, m.Bottom);
                    if (extraTop > m.Top) m = new Thickness(m.Left, extraTop, m.Right, m.Bottom);
                }
                var w = Math.Max(0, ActualWidth - m.Left - m.Right);
                var h = Math.Max(0, ActualHeight - m.Top - m.Bottom);
                return new Rect(m.Left, m.Top, w, h);
            }
        }

        /// <summary>
        /// 자식 차트가 값 라벨(예: BarChart.ShowValues) 을 그릴 때 plot 영역 우/상단을 넘는 경우,
        /// <see cref="AutoFitAxisLabels"/> 가 true 면 그 폭/높이만큼 PlotRect 를 자동으로 줄여서 라벨이 잘리지 않게 한다.
        /// 기본값은 (0, 0) — 영향 없음. BarChart 등이 override.
        /// </summary>
        protected virtual (double extraRight, double extraTop) MeasureValueLabelOverhead() => (0, 0);

        /// <summary>
        /// <see cref="AutoFitAxisLabels"/> 가 true 일 때 categorical 라벨이 차지할 좌/하단 여백을 실측.
        /// Y categorical: 가장 긴 라벨 width + (tick 표식 + 패딩 6) 만큼 좌측 확보.
        /// X categorical: 폰트 height + 패딩(6+축선4) 만큼 하단 확보.
        /// </summary>
        private (double extraLeft, double extraBottom) MeasureCategoricalAxisOverhead()
        {
            double extraLeft = 0, extraBottom = 0;
            var ya = YAxis;
            if (ya != null && ya.IsCategorical && ya.Categories != null && ya.Categories.Count > 0)
            {
                var size = ya.FontSize == 0 ? 11.0 : ya.FontSize;
                var family = ya.FontFamily;
                var weight = ya.FontWeight == default ? FontWeights.Normal : ya.FontWeight;
                var brush = TextBrush ?? Brushes.Gray;
                double maxW = 0;
                foreach (var c in ya.Categories)
                {
                    if (string.IsNullOrEmpty(c)) continue;
                    var ft = TextCache.Get(c, size, brush, family, weight);
                    if (ft.Width > maxW) maxW = ft.Width;
                }
                extraLeft = maxW + 10; // 6 (라벨-축 간격) + 4 (tick)
            }

            var xa = XAxis;
            if (xa != null && xa.IsCategorical && xa.Categories != null && xa.Categories.Count > 0)
            {
                var size = xa.FontSize == 0 ? 11.0 : xa.FontSize;
                extraBottom = size + 10; // 폰트 높이 근사 + 패딩
            }
            return (extraLeft, extraBottom);
        }

        public double DataToViewX(double dx)
        {
            var r = PlotRect;
            var range = ViewMaxX - ViewMinX;
            if (range == 0) return r.Left;
            return r.Left + (dx - ViewMinX) / range * r.Width;
        }

        public double DataToViewY(double dy)
        {
            var r = PlotRect;
            var range = ViewMaxY - ViewMinY;
            if (range == 0) return r.Bottom;
            return r.Bottom - (dy - ViewMinY) / range * r.Height;
        }

        public double ViewToDataX(double vx)
        {
            var r = PlotRect;
            if (r.Width == 0) return ViewMinX;
            return ViewMinX + (vx - r.Left) / r.Width * (ViewMaxX - ViewMinX);
        }

        public double ViewToDataY(double vy)
        {
            var r = PlotRect;
            if (r.Height == 0) return ViewMinY;
            return ViewMinY + (r.Bottom - vy) / r.Height * (ViewMaxY - ViewMinY);
        }

        public Point ScreenToCanvas(Point p) => new Point(ViewToDataX(p.X), ViewToDataY(p.Y));

        #endregion

        #region OnRender lifecycle

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            EnsureDataRange();
            EnsureViewportInitialized();

            DrawBackground(dc);
            if (ShouldDrawAxes())
            {
                DrawGrid(dc);
                DrawAxes(dc);
            }
            DrawPlot(dc);
        }

        protected virtual bool ShouldDrawAxes() => true;

        protected virtual void EnsureDataRange()
        {
            if (!DataRangeDirty) return;
            ComputeDataRange(out DataMinX, out DataMaxX, out DataMinY, out DataMaxY);

            var xa = XAxis;
            if (xa != null)
            {
                if (!xa.IsAutoMin) DataMinX = xa.Min;
                if (!xa.IsAutoMax) DataMaxX = xa.Max;
            }
            var ya = YAxis;
            if (ya != null)
            {
                if (!ya.IsAutoMin) DataMinY = ya.Min;
                if (!ya.IsAutoMax) DataMaxY = ya.Max;
            }

            EnsureFiniteRange(ref DataMinX, ref DataMaxX);
            EnsureFiniteRange(ref DataMinY, ref DataMaxY);

            DataRangeDirty = false;
            ViewportInitialized = false;
        }

        protected virtual void ComputeDataRange(out double minX, out double maxX, out double minY, out double maxY)
        {
            minX = double.PositiveInfinity; maxX = double.NegativeInfinity;
            minY = double.PositiveInfinity; maxY = double.NegativeInfinity;
            var series = Series;
            if (series == null)
            {
                minX = 0; maxX = 1; minY = 0; maxY = 1;
                return;
            }
            foreach (var s in series)
            {
                if (s == null || !s.IsVisible) continue;
                s.GetArrays(out var xs, out var ys, out var n);
                for (var i = 0; i < n; i++)
                {
                    var x = xs[i]; var y = ys[i];
                    if (!double.IsFinite(x) || !double.IsFinite(y)) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (minX == double.PositiveInfinity) { minX = 0; maxX = 1; minY = 0; maxY = 1; }
        }

        protected static void EnsureFiniteRange(ref double min, ref double max)
        {
            if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min)
            {
                if (double.IsFinite(min) && !double.IsFinite(max)) max = min + 1;
                else if (!double.IsFinite(min) && double.IsFinite(max)) min = max - 1;
                else if (min == max) { min -= 0.5; max += 0.5; }
                else { min = 0; max = 1; }
            }
        }

        protected bool ViewportInitialized;

        protected virtual void EnsureViewportInitialized()
        {
            if (ViewportInitialized) return;
            ViewMinX = DataMinX; ViewMaxX = DataMaxX;
            ViewMinY = DataMinY; ViewMaxY = DataMaxY;
            ViewportInitialized = true;
            UpdateZoomLevel();
        }

        protected virtual void DrawBackground(DrawingContext dc)
        {
            var bg = PlotBackgroundBrush;
            if (bg == null) return;
            dc.DrawRectangle(GetFrozenBrush(bg), null, new Rect(0, 0, ActualWidth, ActualHeight));
        }

        protected virtual void DrawGrid(DrawingContext dc)
        {
            var pen = GetPen(GridBrush, 1.0);
            if (pen == null) return;
            var r = PlotRect;
            var xa = XAxis;
            var ya = YAxis;
            var xTickTarget = xa?.MajorTickCountTarget ?? 8;
            var yTickTarget = ya?.MajorTickCountTarget ?? 6;

            if (xa == null || !xa.IsCategorical)
            {
                var xs = ChartTickGenerator.Generate(ViewMinX, ViewMaxX, xTickTarget);
                foreach (var t in xs)
                {
                    var px = SnapPx(DataToViewX(t));
                    if (px < r.Left - 0.5 || px > r.Right + 0.5) continue;
                    dc.DrawLine(pen, new Point(px, r.Top), new Point(px, r.Bottom));
                }
            }

            var ys2 = ChartTickGenerator.Generate(ViewMinY, ViewMaxY, yTickTarget);
            foreach (var t in ys2)
            {
                var py = SnapPx(DataToViewY(t));
                if (py < r.Top - 0.5 || py > r.Bottom + 0.5) continue;
                dc.DrawLine(pen, new Point(r.Left, py), new Point(r.Right, py));
            }
        }

        protected virtual void DrawAxes(DrawingContext dc)
        {
            var axisPen = GetPen(AxisBrush, 1.0);
            var r = PlotRect;
            var textBrush = TextBrush ?? Brushes.Gray;

            if (axisPen != null)
            {
                dc.DrawLine(axisPen, new Point(r.Left, r.Bottom), new Point(r.Right, r.Bottom));
                dc.DrawLine(axisPen, new Point(r.Left, r.Top), new Point(r.Left, r.Bottom));
            }

            var xa = XAxis;
            var ya = YAxis;
            var xFamily = xa?.FontFamily;
            var xWeight = xa?.FontWeight ?? FontWeights.Normal;
            var xSize = xa?.FontSize ?? 11.0;
            var xTitleSize = xa?.TitleFontSize ?? 13.0;
            var yFamily = ya?.FontFamily;
            var yWeight = ya?.FontWeight ?? FontWeights.Normal;
            var ySize = ya?.FontSize ?? 11.0;
            var yTitleSize = ya?.TitleFontSize ?? 13.0;

            if (xa == null || !xa.IsCategorical)
            {
                var xTicks = ChartTickGenerator.Generate(ViewMinX, ViewMaxX, xa?.MajorTickCountTarget ?? 8);
                foreach (var t in xTicks)
                {
                    var px = DataToViewX(t);
                    if (px < r.Left - 0.5 || px > r.Right + 0.5) continue;
                    if (axisPen != null) dc.DrawLine(axisPen, new Point(px, r.Bottom), new Point(px, r.Bottom + 4));
                    var ft = TextCache.Get(xa?.Format(t) ?? t.ToString("0.##"), xSize, textBrush, xFamily, xWeight);
                    dc.DrawText(ft, new Point(px - ft.Width / 2, r.Bottom + 6));
                }
            }
            else if (xa.Categories != null)
            {
                var cats = xa.Categories;
                var n = cats.Count;
                if (n > 0)
                {
                    var slot = r.Width / n;
                    for (var i = 0; i < n; i++)
                    {
                        var px = r.Left + (i + 0.5) * slot;
                        if (px < r.Left - 0.5 || px > r.Right + 0.5) continue;
                        var ft = TextCache.Get(cats[i] ?? string.Empty, xSize, textBrush, xFamily, xWeight);
                        if (ft.Width <= slot * 0.95)
                        {
                            dc.DrawText(ft, new Point(px - ft.Width / 2, r.Bottom + 6));
                        }
                    }
                }
            }

            if (ya == null || !ya.IsCategorical)
            {
                var yTicks = ChartTickGenerator.Generate(ViewMinY, ViewMaxY, ya?.MajorTickCountTarget ?? 6);
                foreach (var t in yTicks)
                {
                    var py = DataToViewY(t);
                    if (py < r.Top - 0.5 || py > r.Bottom + 0.5) continue;
                    if (axisPen != null) dc.DrawLine(axisPen, new Point(r.Left - 4, py), new Point(r.Left, py));
                    var ft = TextCache.Get(ya?.Format(t) ?? t.ToString("0.##"), ySize, textBrush, yFamily, yWeight);
                    dc.DrawText(ft, new Point(r.Left - ft.Width - 6, py - ft.Height / 2));
                }
            }
            else if (ya.Categories != null)
            {
                // X 축 categorical 분기와 대칭 — 슬롯 중앙에 라벨, 슬롯 95% 넘으면 생략(겹침 방지).
                // BarChart Orientation=Horizontal 일 때 catAxis = YAxis 이므로 이 경로에서 그룹명/모델명이 그려진다.
                var cats = ya.Categories;
                var n = cats.Count;
                if (n > 0)
                {
                    var slot = r.Height / n;
                    for (var i = 0; i < n; i++)
                    {
                        var py = r.Top + (i + 0.5) * slot;
                        if (py < r.Top - 0.5 || py > r.Bottom + 0.5) continue;
                        var ft = TextCache.Get(cats[i] ?? string.Empty, ySize, textBrush, yFamily, yWeight);
                        if (ft.Height <= slot * 0.95)
                        {
                            dc.DrawText(ft, new Point(r.Left - ft.Width - 6, py - ft.Height / 2));
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(xa?.Title))
            {
                var ft = TextCache.Get(xa.Title, xTitleSize, textBrush, xFamily, xWeight);
                dc.DrawText(ft, new Point(r.Left + (r.Width - ft.Width) / 2, ActualHeight - ft.Height - 4));
            }
            if (!string.IsNullOrEmpty(ya?.Title))
            {
                var ft = TextCache.Get(ya.Title, yTitleSize, textBrush, yFamily, yWeight);
                dc.PushTransform(new RotateTransform(-90, 12, r.Top + r.Height / 2));
                dc.DrawText(ft, new Point(12 - ft.Width / 2, r.Top + r.Height / 2 - ft.Height / 2));
                dc.Pop();
            }
        }

        protected abstract void DrawPlot(DrawingContext dc);

        #endregion

        #region Mouse / Wheel

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Handled) return;
            if (!IsZoomEnabled) return;
            EnsureDataRange();
            EnsureViewportInitialized();

            var mods = Keyboard.Modifiers;
            var shift = (mods & ModifierKeys.Shift) == ModifierKeys.Shift;
            var ctrl = (mods & ModifierKeys.Control) == ModifierKeys.Control;
            if (shift || ctrl)
            {
                PanByWheel(e, horizontal: ctrl);
                e.Handled = true;
                return;
            }

            var pivotPx = e.GetPosition(this);
            var pivotDx = ViewToDataX(pivotPx.X);
            var pivotDy = ViewToDataY(pivotPx.Y);
            var k = e.Delta > 0 ? 1.1 : 1.0 / 1.1;

            var newMinX = pivotDx - (pivotDx - ViewMinX) / k;
            var newMaxX = pivotDx + (ViewMaxX - pivotDx) / k;
            var newMinY = pivotDy - (pivotDy - ViewMinY) / k;
            var newMaxY = pivotDy + (ViewMaxY - pivotDy) / k;

            if (ApplyClampedView(newMinX, newMaxX, newMinY, newMaxY))
            {
                UpdateZoomLevel();
                InvalidateVisual();
                RaiseViewportChanged();
            }
            e.Handled = true;
        }

        private void PanByWheel(MouseWheelEventArgs e, bool horizontal)
        {
            if (!IsPanEnabled) return;
            var notches = e.Delta / 120.0;
            var r = PlotRect;
            if (horizontal)
            {
                var dataPerView = (ViewMaxX - ViewMinX) / Math.Max(1.0, r.Width);
                var step = -notches * r.Width * 0.1 * dataPerView;
                ViewMinX += step; ViewMaxX += step;
            }
            else
            {
                var dataPerView = (ViewMaxY - ViewMinY) / Math.Max(1.0, r.Height);
                var step = notches * r.Height * 0.1 * dataPerView;
                ViewMinY += step; ViewMaxY += step;
            }
            InvalidateVisual();
            RaiseViewportChanged();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.Handled) return;
            Focus();
            if (!IsPanEnabled) return;
            EnsureDataRange();
            EnsureViewportInitialized();
            _isPanning = true;
            _panStartPx = e.GetPosition(this);
            _panStartViewMinX = ViewMinX; _panStartViewMaxX = ViewMaxX;
            _panStartViewMinY = ViewMinY; _panStartViewMaxY = ViewMaxY;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);

            if (_isPanning)
            {
                var r = PlotRect;
                if (r.Width > 0 && r.Height > 0)
                {
                    var dxData = (pos.X - _panStartPx.X) * (_panStartViewMaxX - _panStartViewMinX) / r.Width;
                    var dyData = (pos.Y - _panStartPx.Y) * (_panStartViewMaxY - _panStartViewMinY) / r.Height;
                    ViewMinX = _panStartViewMinX - dxData;
                    ViewMaxX = _panStartViewMaxX - dxData;
                    ViewMinY = _panStartViewMinY + dyData;
                    ViewMaxY = _panStartViewMaxY + dyData;
                    InvalidateVisual();
                    RaiseViewportChanged();
                }
                return;
            }

            if (IsTooltipEnabled)
            {
                if (HoverMode == ChartHoverMode.XUnified)
                {
                    var unified = HitTestXUnified(pos);
                    XUnifiedHoverChanged?.Invoke(this, unified);
                }
                else
                {
                    var hover = HitTestHover(pos);
                    HoverChanged?.Invoke(this, hover);
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isPanning)
            {
                _isPanning = false;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            HoverChanged?.Invoke(this, ChartHoverState.Empty);
            XUnifiedHoverChanged?.Invoke(this, XUnifiedHoverState.Empty);
        }

        protected virtual XUnifiedHoverState HitTestXUnified(Point screenPx)
        {
            var r = PlotRect;
            if (!r.Contains(screenPx)) return XUnifiedHoverState.Empty;
            var series = Series;
            if (series == null || series.Count == 0) return XUnifiedHoverState.Empty;

            var hoverDataX = ViewToDataX(screenPx.X);
            var points = new List<XUnifiedHoverPoint>(series.Count);
            for (var s = 0; s < series.Count; s++)
            {
                var ser = series[s];
                if (ser == null || !ser.IsVisible) continue;
                ser.GetArrays(out var xs, out var ys, out var n);
                if (n == 0) continue;
                var idx = ChartHitTester.NearestIndexBySortedX(xs, n, hoverDataX);
                if (idx < 0 || idx >= n) continue;
                var dx = xs[idx]; var dy = ys[idx];
                var sp = new Point(DataToViewX(dx), DataToViewY(dy));
                points.Add(new XUnifiedHoverPoint(s, ser.Title, ser.Brush, dx, dy, sp));
            }
            if (points.Count == 0) return XUnifiedHoverState.Empty;
            return new XUnifiedHoverState(hoverDataX, screenPx, points);
        }

        protected virtual ChartHoverState HitTestHover(Point screenPx)
        {
            var r = PlotRect;
            if (!r.Contains(screenPx)) return ChartHoverState.Empty;
            return ChartHitTester.NearestPoint(this, screenPx);
        }

        #endregion

        #region IChartViewport

        double IChartViewport.ZoomLevel => ZoomLevel;
        double IChartViewport.ContentWidth => DataMaxX - DataMinX;
        double IChartViewport.ContentHeight => DataMaxY - DataMinY;
        double IChartViewport.ViewportWidth => ActualWidth;
        double IChartViewport.ViewportHeight => ActualHeight;

        Point IChartViewport.ScreenToCanvas(Point p) => new Point(ViewToDataX(p.X), ViewToDataY(p.Y));

        void IChartViewport.SetOffset(double x, double y)
        {
            EnsureDataRange();
            EnsureViewportInitialized();
            var rangeX = ViewMaxX - ViewMinX;
            var rangeY = ViewMaxY - ViewMinY;
            ViewMinX = x; ViewMaxX = x + rangeX;
            ViewMinY = y; ViewMaxY = y + rangeY;
            InvalidateVisual();
            RaiseViewportChanged();
        }

        void IChartViewport.SetZoom(double zoom)
        {
            if (!double.IsFinite(zoom) || zoom <= 0) return;
            zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
            EnsureDataRange();
            EnsureViewportInitialized();
            var fullX = DataMaxX - DataMinX;
            var fullY = DataMaxY - DataMinY;
            var newRangeX = fullX / zoom;
            var newRangeY = fullY / zoom;
            var cx = (ViewMinX + ViewMaxX) / 2;
            var cy = (ViewMinY + ViewMaxY) / 2;
            ViewMinX = cx - newRangeX / 2; ViewMaxX = cx + newRangeX / 2;
            ViewMinY = cy - newRangeY / 2; ViewMaxY = cy + newRangeY / 2;
            UpdateZoomLevel();
            InvalidateVisual();
            RaiseViewportChanged();
        }

        public void FitToContent()
        {
            EnsureDataRange();
            ViewMinX = DataMinX; ViewMaxX = DataMaxX;
            ViewMinY = DataMinY; ViewMaxY = DataMaxY;
            ViewportInitialized = true;
            UpdateZoomLevel();
            InvalidateVisual();
            RaiseViewportChanged();
        }

        public void ZoomToBounds(Rect bounds, double padding = 0.95)
        {
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;
            EnsureDataRange();
            var pad = padding <= 0 ? 1.0 : padding;
            var cx = bounds.X + bounds.Width / 2;
            var cy = bounds.Y + bounds.Height / 2;
            var w = bounds.Width / pad;
            var h = bounds.Height / pad;
            ViewMinX = cx - w / 2; ViewMaxX = cx + w / 2;
            ViewMinY = cy - h / 2; ViewMaxY = cy + h / 2;
            ViewportInitialized = true;
            UpdateZoomLevel();
            InvalidateVisual();
            RaiseViewportChanged();
        }

        event EventHandler IChartViewport.ViewportChanged
        {
            add => _viewportChanged += value;
            remove => _viewportChanged -= value;
        }

        event SizeChangedEventHandler IChartViewport.ViewportSizeChanged
        {
            add => SizeChangedEx += value;
            remove => SizeChangedEx -= value;
        }

        protected void RaiseViewportChanged()
        {
            _viewportChanged?.Invoke(this, EventArgs.Empty);
            ViewportChangedEx?.Invoke(this, new ViewportChangedEventArgs(ViewMinX, ViewMaxX, ViewMinY, ViewMaxY, ZoomLevel));
        }

        protected void UpdateZoomLevel()
        {
            var fullX = DataMaxX - DataMinX;
            var curX = ViewMaxX - ViewMinX;
            var fullY = DataMaxY - DataMinY;
            var curY = ViewMaxY - ViewMinY;
            double zx = (curX > 0 && fullX > 0) ? fullX / curX : 1.0;
            double zy = (curY > 0 && fullY > 0) ? fullY / curY : 1.0;
            var z = Math.Min(zx, zy);
            if (!double.IsFinite(z) || z <= 0) z = 1.0;
            SetCurrentValue(ZoomLevelProperty, z);
        }

        protected bool ApplyClampedView(double minX, double maxX, double minY, double maxY)
        {
            if (!double.IsFinite(minX) || !double.IsFinite(maxX) || !double.IsFinite(minY) || !double.IsFinite(maxY)) return false;
            if (maxX <= minX || maxY <= minY) return false;
            var fullX = DataMaxX - DataMinX;
            var curX = maxX - minX;
            var zx = (curX > 0 && fullX > 0) ? fullX / curX : 1.0;
            var fullY = DataMaxY - DataMinY;
            var curY = maxY - minY;
            var zy = (curY > 0 && fullY > 0) ? fullY / curY : 1.0;
            var z = Math.Min(zx, zy);
            if (z < MinZoom * 0.999 || z > MaxZoom * 1.001) return false;
            ViewMinX = minX; ViewMaxX = maxX;
            ViewMinY = minY; ViewMaxY = maxY;
            return true;
        }

        #endregion

        #region Brush / Pen helpers

        protected Pen GetPen(Brush brush, double thickness)
        {
            if (brush == null || thickness <= 0) return null;
            var key = (brush, thickness);
            if (_penCache.TryGetValue(key, out var pen)) return pen;
            if (_penCache.Count > 256) _penCache.Clear();
            var fb = GetFrozenBrush(brush);
            pen = new Pen(fb, thickness);
            if (pen.CanFreeze) pen.Freeze();
            _penCache[key] = pen;
            return pen;
        }

        protected Brush GetFrozenBrush(Brush brush)
        {
            if (brush == null) return null;
            if (brush.IsFrozen) return brush;
            if (_frozenBrushCache.TryGetValue(brush, out var fb)) return fb;
            if (brush.CanFreeze)
            {
                var clone = brush.CloneCurrentValue();
                if (clone.CanFreeze) clone.Freeze();
                _frozenBrushCache[brush] = clone;
                return clone;
            }
            return brush;
        }

        protected static double SnapPx(double v) => Math.Round(v) + 0.5;

        #endregion
    }

    public sealed class ViewportChangedEventArgs : EventArgs
    {
        public double ViewMinX { get; }
        public double ViewMaxX { get; }
        public double ViewMinY { get; }
        public double ViewMaxY { get; }
        public double ZoomLevel { get; }
        public ViewportChangedEventArgs(double viewMinX, double viewMaxX, double viewMinY, double viewMaxY, double zoom)
        {
            ViewMinX = viewMinX; ViewMaxX = viewMaxX; ViewMinY = viewMinY; ViewMaxY = viewMaxY; ZoomLevel = zoom;
        }
    }
}
