using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts
{
    public enum PieLabelMode { Percent, Value, Both, None }

    public class PieChart : ChartBase
    {
        public static readonly DependencyProperty IsDoughnutProperty =
            DependencyProperty.Register(nameof(IsDoughnut), typeof(bool), typeof(PieChart),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DoughnutThicknessProperty =
            DependencyProperty.Register(nameof(DoughnutThickness), typeof(double), typeof(PieChart),
                new FrameworkPropertyMetadata(0.4, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StartAngleProperty =
            DependencyProperty.Register(nameof(StartAngle), typeof(double), typeof(PieChart),
                new FrameworkPropertyMetadata(-90.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowLabelsProperty =
            DependencyProperty.Register(nameof(ShowLabels), typeof(bool), typeof(PieChart),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelModeProperty =
            DependencyProperty.Register(nameof(LabelMode), typeof(PieLabelMode), typeof(PieChart),
                new FrameworkPropertyMetadata(PieLabelMode.Percent, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool IsDoughnut { get => (bool)GetValue(IsDoughnutProperty); set => SetValue(IsDoughnutProperty, value); }
        public double DoughnutThickness { get => (double)GetValue(DoughnutThicknessProperty); set => SetValue(DoughnutThicknessProperty, value); }
        public double StartAngle { get => (double)GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
        public bool ShowLabels { get => (bool)GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
        public PieLabelMode LabelMode { get => (PieLabelMode)GetValue(LabelModeProperty); set => SetValue(LabelModeProperty, value); }

        public PieChart()
        {
            IsZoomEnabled = false;
            IsPanEnabled = false;
        }

        protected override bool ShouldDrawAxes() => false;

        protected override void ComputeDataRange(out double minX, out double maxX, out double minY, out double maxY)
        {
            minX = -1; maxX = 1; minY = -1; maxY = 1;
        }

        protected override void DrawPlot(DrawingContext dc)
        {
            var series = Series;
            if (series == null || series.Count == 0) return;
            var r = PlotRect;
            if (r.Width <= 0 || r.Height <= 0) return;

            var values = new List<(int seriesIdx, double value, string label, Brush brush)>();
            var total = 0.0;
            for (var s = 0; s < series.Count; s++)
            {
                var ser = series[s];
                if (ser == null || !ser.IsVisible) continue;
                ser.GetArrays(out var _, out var ys);
                var v = 0.0;
                if (ys != null) foreach (var y in ys) if (double.IsFinite(y)) v += Math.Max(0, y);
                if (v <= 0) continue;
                values.Add((s, v, ser.Title, ser.Brush));
                total += v;
            }
            if (total <= 0) return;

            var cx = r.Left + r.Width / 2;
            var cy = r.Top + r.Height / 2;
            var radius = Math.Min(r.Width, r.Height) / 2 - 8;
            if (radius <= 0) return;
            var innerR = IsDoughnut ? radius * (1 - Math.Max(0.05, Math.Min(0.9, DoughnutThickness))) : 0;

            var startDeg = StartAngle;
            var textBrush = TextBrush ?? Brushes.Gray;

            foreach (var item in values)
            {
                var sweep = item.value / total * 360.0;
                var endDeg = startDeg + sweep;
                var fb = GetFrozenBrush(item.brush);
                var geo = BuildSliceGeometry(cx, cy, radius, innerR, startDeg, endDeg);
                if (geo != null) dc.DrawGeometry(fb, null, geo);

                if (ShowLabels && LabelMode != PieLabelMode.None && sweep / 360.0 >= 0.05)
                {
                    var midRad = (startDeg + endDeg) / 2 * Math.PI / 180;
                    var labelR = (innerR + radius) / 2;
                    if (innerR <= 0) labelR = radius * 0.65;
                    var lx = cx + Math.Cos(midRad) * labelR;
                    var ly = cy + Math.Sin(midRad) * labelR;
                    string text = LabelMode switch
                    {
                        PieLabelMode.Percent => $"{(item.value / total * 100):0.#}%",
                        PieLabelMode.Value => item.value.ToString("0.##"),
                        PieLabelMode.Both => $"{item.value:0.##} ({item.value / total * 100:0.#}%)",
                        _ => string.Empty
                    };
                    if (!string.IsNullOrEmpty(text))
                    {
                        var ft = TextCache.Get(text, 11, textBrush);
                        dc.DrawText(ft, new Point(lx - ft.Width / 2, ly - ft.Height / 2));
                    }
                }
                startDeg = endDeg;
            }
        }

        private static Geometry BuildSliceGeometry(double cx, double cy, double rOuter, double rInner, double startDeg, double endDeg)
        {
            var sweep = endDeg - startDeg;
            if (sweep <= 0) return null;
            if (sweep >= 360) sweep = 359.999;
            var startRad = startDeg * Math.PI / 180;
            var endRad = endDeg * Math.PI / 180;
            var isLarge = sweep > 180;
            var p0Outer = new Point(cx + Math.Cos(startRad) * rOuter, cy + Math.Sin(startRad) * rOuter);
            var p1Outer = new Point(cx + Math.Cos(endRad) * rOuter, cy + Math.Sin(endRad) * rOuter);

            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                if (rInner <= 0)
                {
                    ctx.BeginFigure(new Point(cx, cy), true, true);
                    ctx.LineTo(p0Outer, false, false);
                    ctx.ArcTo(p1Outer, new Size(rOuter, rOuter), 0, isLarge, SweepDirection.Clockwise, false, false);
                    ctx.LineTo(new Point(cx, cy), false, false);
                }
                else
                {
                    var p1Inner = new Point(cx + Math.Cos(endRad) * rInner, cy + Math.Sin(endRad) * rInner);
                    var p0Inner = new Point(cx + Math.Cos(startRad) * rInner, cy + Math.Sin(startRad) * rInner);
                    ctx.BeginFigure(p0Outer, true, true);
                    ctx.ArcTo(p1Outer, new Size(rOuter, rOuter), 0, isLarge, SweepDirection.Clockwise, false, false);
                    ctx.LineTo(p1Inner, false, false);
                    ctx.ArcTo(p0Inner, new Size(rInner, rInner), 0, isLarge, SweepDirection.Counterclockwise, false, false);
                    ctx.LineTo(p0Outer, false, false);
                }
            }
            sg.Freeze();
            return sg;
        }

        protected override ChartHoverState HitTestHover(Point screenPx)
        {
            var series = Series;
            if (series == null) return ChartHoverState.Empty;
            var r = PlotRect;
            var cx = r.Left + r.Width / 2;
            var cy = r.Top + r.Height / 2;
            var radius = Math.Min(r.Width, r.Height) / 2 - 8;
            if (radius <= 0) return ChartHoverState.Empty;
            var innerR = IsDoughnut ? radius * (1 - Math.Max(0.05, Math.Min(0.9, DoughnutThickness))) : 0;
            var dx = screenPx.X - cx; var dy = screenPx.Y - cy;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < innerR || dist > radius) return ChartHoverState.Empty;

            var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
            while (angle < StartAngle) angle += 360;
            var rel = angle - StartAngle;

            var total = 0.0;
            var values = new List<(int seriesIdx, double v, string title)>();
            for (var s = 0; s < series.Count; s++)
            {
                var ser = series[s];
                if (ser == null || !ser.IsVisible) continue;
                ser.GetArrays(out var _, out var ys);
                var v = 0.0;
                if (ys != null) foreach (var y in ys) if (double.IsFinite(y)) v += Math.Max(0, y);
                if (v <= 0) continue;
                values.Add((s, v, ser.Title));
                total += v;
            }
            if (total <= 0) return ChartHoverState.Empty;
            var sweep = 0.0;
            foreach (var item in values)
            {
                var s = item.v / total * 360.0;
                if (rel >= sweep && rel < sweep + s)
                    return new ChartHoverState(item.seriesIdx, 0, item.v, item.v / total * 100, screenPx, item.title);
                sweep += s;
            }
            return ChartHoverState.Empty;
        }
    }
}
