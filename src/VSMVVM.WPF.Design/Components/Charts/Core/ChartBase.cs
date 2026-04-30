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

        public IList<ChartSeries> Series { get => (IList<ChartSeries>)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
        public ChartAxis XAxis { get => (ChartAxis)GetValue(XAxisProperty); set => SetValue(XAxisProperty, value); }
        public ChartAxis YAxis { get => (ChartAxis)GetValue(YAxisProperty); set => SetValue(YAxisProperty, value); }
        public Thickness PlotMargin { get => (Thickness)GetValue(PlotMarginProperty); set => SetValue(PlotMarginProperty, value); }
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
            SizeChanged += (s, e) => SizeChangedEx?.Invoke(s, e);
        }

        private void OnLoadedSetDpi(object sender, RoutedEventArgs e)
        {
            try { TextCache.SetPixelsPerDip(VisualTreeHelper.GetDpi(this).PixelsPerDip); }
            catch { TextCache.SetPixelsPerDip(1.0); }
        }

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
                cb.InvalidateVisual();
            }
        }

        private void OnSeriesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null) UnsubscribeSeries(e.OldItems);
            if (e.NewItems != null) SubscribeSeries(e.NewItems);
            AssignDefaultSeriesBrushes();
            DataRangeDirty = true;
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
            InvalidateVisual();
        }

        private void OnSeriesItemVisualChanged(object sender, EventArgs e) => InvalidateVisual();

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
                var w = Math.Max(0, ActualWidth - PlotMargin.Left - PlotMargin.Right);
                var h = Math.Max(0, ActualHeight - PlotMargin.Top - PlotMargin.Bottom);
                return new Rect(PlotMargin.Left, PlotMargin.Top, w, h);
            }
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

            var yTicks = ChartTickGenerator.Generate(ViewMinY, ViewMaxY, ya?.MajorTickCountTarget ?? 6);
            foreach (var t in yTicks)
            {
                var py = DataToViewY(t);
                if (py < r.Top - 0.5 || py > r.Bottom + 0.5) continue;
                if (axisPen != null) dc.DrawLine(axisPen, new Point(r.Left - 4, py), new Point(r.Left, py));
                var ft = TextCache.Get(ya?.Format(t) ?? t.ToString("0.##"), ySize, textBrush, yFamily, yWeight);
                dc.DrawText(ft, new Point(r.Left - ft.Width - 6, py - ft.Height / 2));
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
