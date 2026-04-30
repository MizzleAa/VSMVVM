using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using VSMVVM.WPF.Design.Components.Charts.Core;
using VSMVVM.WPF.Design.Components.Charts.Downsamplers;

namespace VSMVVM.WPF.Design.Components.Charts
{
    public enum LineMode { Straight, Stepped }

    public class LineChart : ChartBase
    {
        public static readonly DependencyProperty LineModeProperty =
            DependencyProperty.Register(nameof(LineMode), typeof(LineMode), typeof(LineChart),
                new FrameworkPropertyMetadata(LineMode.Straight, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsAreaFilledProperty =
            DependencyProperty.Register(nameof(IsAreaFilled), typeof(bool), typeof(LineChart),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AreaFillOpacityProperty =
            DependencyProperty.Register(nameof(AreaFillOpacity), typeof(double), typeof(LineChart),
                new FrameworkPropertyMetadata(0.2, FrameworkPropertyMetadataOptions.AffectsRender));

        public LineMode LineMode { get => (LineMode)GetValue(LineModeProperty); set => SetValue(LineModeProperty, value); }
        public bool IsAreaFilled { get => (bool)GetValue(IsAreaFilledProperty); set => SetValue(IsAreaFilledProperty, value); }
        public double AreaFillOpacity { get => (double)GetValue(AreaFillOpacityProperty); set => SetValue(AreaFillOpacityProperty, value); }

        private readonly LttbDownsampler _lttb = new();
        private readonly List<(double[] xs, double[] ys)> _downsampled = new();
        private long _lastViewportHash;

        protected override void DrawPlot(DrawingContext dc)
        {
            EnsureDownsampled();
            var r = PlotRect;
            if (r.Width <= 0 || r.Height <= 0) return;
            var series = Series;
            if (series == null) return;

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
                    if (n < 1) continue;

                    var brush = ser.Brush;
                    var pen = GetPen(brush, ser.StrokeThickness);
                    if (pen == null) continue;

                    var pts = new Point[n];
                    for (var i = 0; i < n; i++) pts[i] = new Point(DataToViewX(xs[i]), DataToViewY(ys[i]));

                    var geo = BuildLineGeometry(pts, n, LineMode);
                    if (geo != null)
                    {
                        if (IsAreaFilled && n >= 2)
                        {
                            var areaGeo = BuildAreaGeometry(pts, n, r, LineMode);
                            if (areaGeo != null)
                            {
                                var fb = GetFrozenBrush(brush);
                                if (fb is SolidColorBrush scb)
                                {
                                    var c = scb.Color;
                                    var alpha = (byte)Math.Max(0, Math.Min(255, AreaFillOpacity * 255));
                                    var fill = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
                                    fill.Freeze();
                                    dc.DrawGeometry(fill, null, areaGeo);
                                }
                            }
                        }
                        dc.DrawGeometry(null, pen, geo);
                    }

                    if (ser.MarkerSize > 0)
                    {
                        var fb = GetFrozenBrush(brush);
                        var ms = ser.MarkerSize;
                        for (var i = 0; i < n; i++)
                        {
                            var p = pts[i];
                            if (p.X < r.Left - ms || p.X > r.Right + ms) continue;
                            if (p.Y < r.Top - ms || p.Y > r.Bottom + ms) continue;
                            dc.DrawEllipse(fb, null, p, ms / 2, ms / 2);
                        }
                    }
                }
            }
            finally
            {
                dc.Pop();
            }
        }

        private static Geometry BuildLineGeometry(Point[] pts, int n, LineMode mode)
        {
            if (n < 1) return null;
            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                ctx.BeginFigure(pts[0], false, false);
                if (mode == LineMode.Straight)
                {
                    var arr = new Point[n - 1];
                    for (var i = 1; i < n; i++) arr[i - 1] = pts[i];
                    ctx.PolyLineTo(arr, true, false);
                }
                else
                {
                    var arr = new List<Point>((n - 1) * 2);
                    for (var i = 1; i < n; i++)
                    {
                        arr.Add(new Point(pts[i].X, pts[i - 1].Y));
                        arr.Add(pts[i]);
                    }
                    ctx.PolyLineTo(arr, true, false);
                }
            }
            sg.Freeze();
            return sg;
        }

        private static Geometry BuildAreaGeometry(Point[] pts, int n, Rect plotRect, LineMode mode)
        {
            if (n < 2) return null;
            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                ctx.BeginFigure(new Point(pts[0].X, plotRect.Bottom), true, true);
                ctx.LineTo(pts[0], false, false);
                if (mode == LineMode.Straight)
                {
                    var arr = new Point[n - 1];
                    for (var i = 1; i < n; i++) arr[i - 1] = pts[i];
                    ctx.PolyLineTo(arr, false, false);
                }
                else
                {
                    var arr = new List<Point>((n - 1) * 2);
                    for (var i = 1; i < n; i++)
                    {
                        arr.Add(new Point(pts[i].X, pts[i - 1].Y));
                        arr.Add(pts[i]);
                    }
                    ctx.PolyLineTo(arr, false, false);
                }
                ctx.LineTo(new Point(pts[n - 1].X, plotRect.Bottom), false, false);
            }
            sg.Freeze();
            return sg;
        }

        private void EnsureDownsampled()
        {
            var hash = ComputeViewportHash();
            var series = Series;
            if (hash == _lastViewportHash && series != null && _downsampled.Count == series.Count) return;

            _lastViewportHash = hash;
            _downsampled.Clear();
            if (series == null) return;

            var r = PlotRect;
            var targetBuckets = Math.Max(2, (int)r.Width);

            foreach (var ser in series)
            {
                if (ser == null) { _downsampled.Add((Array.Empty<double>(), Array.Empty<double>())); continue; }
                ser.GetArrays(out var xs, out var ys, out var n);
                if (n == 0) { _downsampled.Add((Array.Empty<double>(), Array.Empty<double>())); continue; }

                if (n > targetBuckets * 4)
                {
                    _lttb.Downsample(xs, ys, 0, n, targetBuckets, out var dxs, out var dys);
                    _downsampled.Add((dxs, dys));
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

    }
}
