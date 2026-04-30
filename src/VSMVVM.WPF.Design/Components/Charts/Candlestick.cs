using System;
using System.Windows;
using System.Windows.Media;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts
{
    public class Candlestick : ChartBase
    {
        public static readonly DependencyProperty CandleSeriesProperty =
            DependencyProperty.Register(nameof(CandleSeries), typeof(CandleSeries), typeof(Candlestick),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsRender, OnCandleSeriesChanged));

        public static readonly DependencyProperty BodyWidthRatioProperty =
            DependencyProperty.Register(nameof(BodyWidthRatio), typeof(double), typeof(Candlestick),
                new FrameworkPropertyMetadata(0.7, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WickThicknessProperty =
            DependencyProperty.Register(nameof(WickThickness), typeof(double), typeof(Candlestick),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BullBrushProperty =
            DependencyProperty.Register(nameof(BullBrush), typeof(Brush), typeof(Candlestick),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BearBrushProperty =
            DependencyProperty.Register(nameof(BearBrush), typeof(Brush), typeof(Candlestick),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public CandleSeries CandleSeries { get => (CandleSeries)GetValue(CandleSeriesProperty); set => SetValue(CandleSeriesProperty, value); }
        public double BodyWidthRatio { get => (double)GetValue(BodyWidthRatioProperty); set => SetValue(BodyWidthRatioProperty, value); }
        public double WickThickness { get => (double)GetValue(WickThicknessProperty); set => SetValue(WickThicknessProperty, value); }
        public Brush BullBrush { get => (Brush)GetValue(BullBrushProperty); set => SetValue(BullBrushProperty, value); }
        public Brush BearBrush { get => (Brush)GetValue(BearBrushProperty); set => SetValue(BearBrushProperty, value); }

        private static void OnCandleSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Candlestick c)
            {
                if (e.OldValue is CandleSeries oldS)
                {
                    oldS.DataChanged -= c.OnSeriesDataChanged;
                    oldS.VisualChanged -= c.OnSeriesVisualChanged;
                }
                if (e.NewValue is CandleSeries newS)
                {
                    newS.DataChanged += c.OnSeriesDataChanged;
                    newS.VisualChanged += c.OnSeriesVisualChanged;
                }
                c.DataRangeDirty = true;
            }
        }

        private void OnSeriesDataChanged(object sender, EventArgs e)
        {
            DataRangeDirty = true;
            InvalidateVisual();
        }

        private void OnSeriesVisualChanged(object sender, EventArgs e) => InvalidateVisual();

        protected override void ComputeDataRange(out double minX, out double maxX, out double minY, out double maxY)
        {
            var series = CandleSeries;
            if (series == null) { minX = 0; maxX = 1; minY = 0; maxY = 1; return; }
            series.GetArrays(out var o, out var h, out var l, out var c);
            var n = series.Count;
            if (n == 0) { minX = 0; maxX = 1; minY = 0; maxY = 1; return; }

            minX = -0.5; maxX = n - 0.5;
            minY = double.PositiveInfinity;
            maxY = double.NegativeInfinity;
            for (var i = 0; i < n; i++)
            {
                var lo = l[i]; var hi = h[i];
                if (double.IsFinite(lo) && lo < minY) minY = lo;
                if (double.IsFinite(hi) && hi > maxY) maxY = hi;
            }
            if (!double.IsFinite(minY) || !double.IsFinite(maxY) || maxY <= minY)
            {
                minY = 0; maxY = 1;
            }
            else
            {
                var pad = (maxY - minY) * 0.05;
                minY -= pad; maxY += pad;
            }
        }

        protected override void DrawPlot(DrawingContext dc)
        {
            var series = CandleSeries;
            if (series == null) return;
            series.GetArrays(out var o, out var h, out var l, out var c);
            var n = series.Count;
            if (n == 0) return;

            var r = PlotRect;
            if (r.Width <= 0 || r.Height <= 0) return;

            var bullBrush = series.BullBrush ?? BullBrush ?? (TryFindResource("Green500Brush") as Brush) ?? Brushes.Green;
            var bearBrush = series.BearBrush ?? BearBrush ?? (TryFindResource("Red500Brush") as Brush) ?? Brushes.Red;
            bullBrush = GetFrozenBrush(bullBrush);
            bearBrush = GetFrozenBrush(bearBrush);

            // 봉 한 칸당 픽셀 폭 — viewport 기준
            var pxPerIndex = r.Width / Math.Max(1e-12, ViewMaxX - ViewMinX);
            var bodyW = Math.Max(1.0, pxPerIndex * BodyWidthRatio);

            var visStart = (int)Math.Floor(ViewMinX);
            var visEnd = (int)Math.Ceiling(ViewMaxX) + 1;
            if (visStart < 0) visStart = 0;
            if (visEnd > n) visEnd = n;

            dc.PushClip(new RectangleGeometry(r));
            try
            {
                for (var i = visStart; i < visEnd; i++)
                {
                    var op = o[i]; var hi = h[i]; var lo = l[i]; var cl = c[i];
                    if (!double.IsFinite(op) || !double.IsFinite(hi) || !double.IsFinite(lo) || !double.IsFinite(cl)) continue;

                    var bullish = cl >= op;
                    var brush = bullish ? bullBrush : bearBrush;
                    var pen = GetPen(brush, WickThickness);

                    var xCenter = DataToViewX(i);
                    if (xCenter < r.Left - bodyW || xCenter > r.Right + bodyW) continue;

                    // wick (high-low 수직선)
                    if (pen != null)
                        dc.DrawLine(pen, new Point(xCenter, DataToViewY(hi)), new Point(xCenter, DataToViewY(lo)));

                    // body — 봉 폭이 1px 이하면 스킵 (wick만)
                    if (bodyW >= 1.0)
                    {
                        var top = Math.Max(op, cl);
                        var bot = Math.Min(op, cl);
                        var yTop = DataToViewY(top);
                        var yBot = DataToViewY(bot);
                        var bodyHeight = Math.Max(1.0, yBot - yTop);
                        var rect = new Rect(xCenter - bodyW / 2, yTop, bodyW, bodyHeight);
                        dc.DrawRectangle(brush, null, rect);
                    }
                }
            }
            finally { dc.Pop(); }
        }

        protected override ChartHoverState HitTestHover(Point screenPx)
        {
            var series = CandleSeries;
            if (series == null) return ChartHoverState.Empty;
            var n = series.Count;
            if (n == 0) return ChartHoverState.Empty;
            var r = PlotRect;
            if (!r.Contains(screenPx)) return ChartHoverState.Empty;

            var dataX = ViewToDataX(screenPx.X);
            var idx = (int)Math.Round(dataX);
            if (idx < 0 || idx >= n) return ChartHoverState.Empty;

            series.GetArrays(out var o, out var h, out var l, out var c);
            var op = o[idx]; var hi = h[idx]; var lo = l[idx]; var cl = c[idx];
            var tag = new CandleHoverInfo(idx, op, hi, lo, cl,
                                          series.Times != null && idx < series.Times.Count ? series.Times[idx] : (DateTime?)null);
            var sp = new Point(DataToViewX(idx), DataToViewY(cl));
            return new ChartHoverState(0, idx, idx, cl, sp, series.Title, tag);
        }
    }

    public sealed class CandleHoverInfo
    {
        public int Index { get; }
        public double Open { get; }
        public double High { get; }
        public double Low { get; }
        public double Close { get; }
        public DateTime? Time { get; }

        public CandleHoverInfo(int index, double open, double high, double low, double close, DateTime? time)
        {
            Index = index; Open = open; High = high; Low = low; Close = close; Time = time;
        }
    }
}
