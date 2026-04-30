using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using VSMVVM.WPF.Design.Components.Charts.Core;
using VSMVVM.WPF.Design.Components.Charts.Downsamplers;

namespace VSMVVM.WPF.Design.Components.Charts
{
    public class ScatterChart : ChartBase
    {
        public static readonly DependencyProperty DefaultMarkerSizeProperty =
            DependencyProperty.Register(nameof(DefaultMarkerSize), typeof(double), typeof(ScatterChart),
                new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DefaultMarkerShapeProperty =
            DependencyProperty.Register(nameof(DefaultMarkerShape), typeof(MarkerShape), typeof(ScatterChart),
                new FrameworkPropertyMetadata(MarkerShape.Circle, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DensityModeProperty =
            DependencyProperty.Register(nameof(DensityMode), typeof(ScatterDensityMode), typeof(ScatterChart),
                new FrameworkPropertyMetadata(ScatterDensityMode.Off, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ColorMapProperty =
            DependencyProperty.Register(nameof(ColorMap), typeof(ColorMap), typeof(ScatterChart),
                new FrameworkPropertyMetadata(ColorMap.Viridis, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowDensityColorBarProperty =
            DependencyProperty.Register(nameof(ShowDensityColorBar), typeof(bool), typeof(ScatterChart),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public double DefaultMarkerSize { get => (double)GetValue(DefaultMarkerSizeProperty); set => SetValue(DefaultMarkerSizeProperty, value); }
        public MarkerShape DefaultMarkerShape { get => (MarkerShape)GetValue(DefaultMarkerShapeProperty); set => SetValue(DefaultMarkerShapeProperty, value); }
        public ScatterDensityMode DensityMode { get => (ScatterDensityMode)GetValue(DensityModeProperty); set => SetValue(DensityModeProperty, value); }
        public ColorMap ColorMap { get => (ColorMap)GetValue(ColorMapProperty); set => SetValue(ColorMapProperty, value); }
        public bool ShowDensityColorBar { get => (bool)GetValue(ShowDensityColorBarProperty); set => SetValue(ShowDensityColorBarProperty, value); }

        private readonly List<(double[] xs, double[] ys)> _downsampled = new();
        private long _lastViewportHash;

        protected override void DrawPlot(DrawingContext dc)
        {
            var r = PlotRect;
            if (r.Width <= 0 || r.Height <= 0) return;
            var series = Series;
            if (series == null) return;

            if (DensityMode != ScatterDensityMode.Off)
            {
                DrawDensity(dc, r);
                return;
            }

            EnsureDownsampled();

            dc.PushClip(new RectangleGeometry(r));
            try
            {
                for (var s = 0; s < series.Count; s++)
                {
                    var ser = series[s];
                    if (ser == null || !ser.IsVisible) continue;
                    if (s >= _downsampled.Count) continue;
                    var (xs, ys) = _downsampled[s];
                    var n = Math.Min(xs?.Length ?? 0, ys?.Length ?? 0);
                    if (n == 0) continue;

                    var fb = GetFrozenBrush(ser.Brush);
                    var size = ser.MarkerSize > 0 ? ser.MarkerSize : DefaultMarkerSize;
                    var shape = ser.MarkerShape;
                    var half = size / 2;

                    for (var i = 0; i < n; i++)
                    {
                        var p = new Point(DataToViewX(xs[i]), DataToViewY(ys[i]));
                        DrawMarker(dc, fb, p, half, shape);
                    }
                }
            }
            finally
            {
                dc.Pop();
            }
        }

        private static void DrawMarker(DrawingContext dc, Brush brush, Point p, double half, MarkerShape shape)
        {
            switch (shape)
            {
                case MarkerShape.Square:
                    dc.DrawRectangle(brush, null, new Rect(p.X - half, p.Y - half, half * 2, half * 2));
                    break;
                case MarkerShape.Triangle:
                    var tri = new StreamGeometry();
                    using (var c = tri.Open())
                    {
                        c.BeginFigure(new Point(p.X, p.Y - half), true, true);
                        c.LineTo(new Point(p.X + half, p.Y + half), false, false);
                        c.LineTo(new Point(p.X - half, p.Y + half), false, false);
                    }
                    tri.Freeze();
                    dc.DrawGeometry(brush, null, tri);
                    break;
                case MarkerShape.Diamond:
                    var dia = new StreamGeometry();
                    using (var c = dia.Open())
                    {
                        c.BeginFigure(new Point(p.X, p.Y - half), true, true);
                        c.LineTo(new Point(p.X + half, p.Y), false, false);
                        c.LineTo(new Point(p.X, p.Y + half), false, false);
                        c.LineTo(new Point(p.X - half, p.Y), false, false);
                    }
                    dia.Freeze();
                    dc.DrawGeometry(brush, null, dia);
                    break;
                case MarkerShape.Cross:
                    var pen = new Pen(brush, Math.Max(1.0, half * 0.4));
                    pen.Freeze();
                    dc.DrawLine(pen, new Point(p.X - half, p.Y - half), new Point(p.X + half, p.Y + half));
                    dc.DrawLine(pen, new Point(p.X - half, p.Y + half), new Point(p.X + half, p.Y - half));
                    break;
                default:
                    dc.DrawEllipse(brush, null, p, half, half);
                    break;
            }
        }

        private void EnsureDownsampled()
        {
            var hash = ComputeViewportHash();
            var series = Series;
            if (hash == _lastViewportHash && series != null && _downsampled.Count == series.Count) return;

            _lastViewportHash = hash;
            _downsampled.Clear();
            if (series == null) return;

            var dataRangeX = Math.Max(1e-12, DataMaxX - DataMinX);
            var dataRangeY = Math.Max(1e-12, DataMaxY - DataMinY);
            var viewW = Math.Max(1.0, PlotRect.Width);
            var viewH = Math.Max(1.0, PlotRect.Height);

            foreach (var ser in series)
            {
                if (ser == null) { _downsampled.Add((Array.Empty<double>(), Array.Empty<double>())); continue; }
                ser.GetArrays(out var xs, out var ys, out var n);
                if (n == 0) { _downsampled.Add((Array.Empty<double>(), Array.Empty<double>())); continue; }

                if (n > 5000)
                {
                    var seen = new System.Collections.Generic.HashSet<long>(Math.Min(n, 65536));
                    var ox = new System.Collections.Generic.List<double>(Math.Min(n, 65536));
                    var oy = new System.Collections.Generic.List<double>(Math.Min(n, 65536));
                    for (var i = 0; i < n; i++)
                    {
                        var x = xs[i]; var y = ys[i];
                        if (!double.IsFinite(x) || !double.IsFinite(y)) continue;
                        var px = (int)((x - DataMinX) / dataRangeX * viewW);
                        var py = (int)((y - DataMinY) / dataRangeY * viewH);
                        long key = ((long)px << 20) | (uint)py;
                        if (seen.Add(key)) { ox.Add(x); oy.Add(y); }
                    }
                    _downsampled.Add((ox.ToArray(), oy.ToArray()));
                }
                else
                {
                    var sliceXs = new double[n];
                    var sliceYs = new double[n];
                    Array.Copy(xs, 0, sliceXs, 0, n);
                    Array.Copy(ys, 0, sliceYs, 0, n);
                    _downsampled.Add((sliceXs, sliceYs));
                }
            }
        }

        private long ComputeViewportHash()
        {
            unchecked
            {
                var h = 17L;
                h = h * 31 + (Series?.Count ?? 0);
                h = h * 31 + ((int)PlotRect.Width);
                h = h * 31 + ((int)PlotRect.Height);
                h = h * 31 + DataMinX.GetHashCode();
                h = h * 31 + DataMaxX.GetHashCode();
                h = h * 31 + DataMinY.GetHashCode();
                h = h * 31 + DataMaxY.GetHashCode();
                if (Series != null)
                {
                    foreach (var s in Series)
                    {
                        if (s == null) continue;
                        h = h * 31 + s.Count;
                    }
                }
                return h;
            }
        }

        private void DrawDensity(DrawingContext dc, Rect r)
        {
            var viewW = (int)Math.Ceiling(r.Width);
            var viewH = (int)Math.Ceiling(r.Height);
            if (viewW <= 0 || viewH <= 0) return;

            var rangeX = ViewMaxX - ViewMinX;
            var rangeY = ViewMaxY - ViewMinY;
            if (rangeX <= 0 || rangeY <= 0) return;

            var counts = new int[viewW * viewH];
            var maxCount = 0;
            var series = Series;

            foreach (var ser in series)
            {
                if (ser == null || !ser.IsVisible) continue;
                ser.GetArrays(out var xs, out var ys, out var n);
                for (var i = 0; i < n; i++)
                {
                    var x = xs[i]; var y = ys[i];
                    if (!double.IsFinite(x) || !double.IsFinite(y)) continue;
                    var px = (int)((x - ViewMinX) / rangeX * viewW);
                    var py = (int)((ViewMaxY - y) / rangeY * viewH);
                    if (px < 0 || py < 0 || px >= viewW || py >= viewH) continue;
                    var idx = py * viewW + px;
                    counts[idx]++;
                    if (counts[idx] > maxCount) maxCount = counts[idx];
                }
            }
            if (maxCount == 0) return;

            var mode = DensityMode;
            var colorMap = ColorMap;
            var baseColor = (Series.Count > 0 ? (Series[0]?.Brush as SolidColorBrush)?.Color : null)
                            ?? Color.FromRgb(59, 130, 246);
            var logDenom = Math.Log(maxCount + 1);

            dc.PushClip(new RectangleGeometry(r));
            try
            {
                for (var py = 0; py < viewH; py++)
                {
                    for (var px = 0; px < viewW; px++)
                    {
                        var c = counts[py * viewW + px];
                        if (c == 0) continue;
                        var t = Math.Log(c + 1) / logDenom;
                        Color color;
                        if (mode == ScatterDensityMode.Heatmap)
                        {
                            color = ColorMaps.Sample(colorMap, t);
                        }
                        else // Alpha
                        {
                            var alpha = (byte)(t * 255);
                            color = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
                        }
                        var brush = new SolidColorBrush(color); brush.Freeze();
                        dc.DrawRectangle(brush, null, new Rect(r.Left + px, r.Top + py, 1, 1));
                    }
                }
            }
            finally { dc.Pop(); }

            if (ShowDensityColorBar)
            {
                LinearGradientBrush grad;
                if (mode == ScatterDensityMode.Heatmap)
                {
                    grad = ColorMaps.CreateGradientBrush(colorMap, vertical: true);
                }
                else
                {
                    var solid = baseColor;
                    var transparent = Color.FromArgb(0, solid.R, solid.G, solid.B);
                    grad = ColorMaps.CreateCustomGradient(transparent, solid, vertical: true);
                }
                var textBrush = TextBrush ?? Brushes.Gray;
                ColorBarRenderer.Draw(dc, r, ActualWidth, grad, 0, maxCount, textBrush, TextCache);
            }
        }

        /// <summary>
        /// Scatter는 X 정렬이 안 되어 있어 base의 binary search 기반 XUnified hit-test가 부정확.
        /// 마우스 픽셀 X ± PickRadiusPx 범위에서 각 시리즈의 마우스 Y에 가장 가까운 점을 선택한다.
        /// 100k 처리 위해 _downsampled 캐시(픽셀 버킷팅된 결과)에서 검색.
        /// </summary>
        protected override XUnifiedHoverState HitTestXUnified(Point screenPx)
        {
            var r = PlotRect;
            if (!r.Contains(screenPx)) return XUnifiedHoverState.Empty;
            var series = Series;
            if (series == null || series.Count == 0) return XUnifiedHoverState.Empty;

            EnsureDownsampled();

            const double PickRadiusPx = 12.0;
            var points = new List<XUnifiedHoverPoint>(series.Count);

            for (var s = 0; s < series.Count; s++)
            {
                var ser = series[s];
                if (ser == null || !ser.IsVisible) continue;

                double[] xs, ys;
                int n;
                if (s < _downsampled.Count && _downsampled[s].xs != null && _downsampled[s].xs.Length > 0)
                {
                    var (dxs, dys) = _downsampled[s];
                    xs = dxs; ys = dys; n = dxs.Length;
                }
                else
                {
                    ser.GetArrays(out xs, out ys, out n);
                }
                if (n == 0) continue;

                var bestDist = double.MaxValue;
                var bestIdx = -1;
                for (var i = 0; i < n; i++)
                {
                    var px = DataToViewX(xs[i]);
                    var dx = px - screenPx.X;
                    if (dx < -PickRadiusPx || dx > PickRadiusPx) continue;
                    var py = DataToViewY(ys[i]);
                    var dy = py - screenPx.Y;
                    var d = dx * dx + dy * dy;
                    if (d < bestDist) { bestDist = d; bestIdx = i; }
                }
                if (bestIdx < 0) continue;
                var dataX = xs[bestIdx];
                var dataY = ys[bestIdx];
                var sp = new Point(DataToViewX(dataX), DataToViewY(dataY));
                points.Add(new XUnifiedHoverPoint(s, ser.Title, ser.Brush, dataX, dataY, sp));
            }
            if (points.Count == 0) return XUnifiedHoverState.Empty;
            var hoverDataX = ViewToDataX(screenPx.X);
            return new XUnifiedHoverState(hoverDataX, screenPx, points);
        }
    }
}
