using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts
{
    public class Histogram : ChartBase
    {
        public static readonly DependencyProperty BinCountProperty =
            DependencyProperty.Register(nameof(BinCount), typeof(int), typeof(Histogram),
                new FrameworkPropertyMetadata(30, FrameworkPropertyMetadataOptions.AffectsRender, OnBinningChanged));

        public static readonly DependencyProperty BinSizeProperty =
            DependencyProperty.Register(nameof(BinSize), typeof(double), typeof(Histogram),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender, OnBinningChanged));

        public static readonly DependencyProperty BarOpacityProperty =
            DependencyProperty.Register(nameof(BarOpacity), typeof(double), typeof(Histogram),
                new FrameworkPropertyMetadata(0.6, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RangeMinProperty =
            DependencyProperty.Register(nameof(RangeMin), typeof(double), typeof(Histogram),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender, OnBinningChanged));

        public static readonly DependencyProperty RangeMaxProperty =
            DependencyProperty.Register(nameof(RangeMax), typeof(double), typeof(Histogram),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender, OnBinningChanged));

        public int BinCount { get => (int)GetValue(BinCountProperty); set => SetValue(BinCountProperty, value); }
        public double BinSize { get => (double)GetValue(BinSizeProperty); set => SetValue(BinSizeProperty, value); }
        public double BarOpacity { get => (double)GetValue(BarOpacityProperty); set => SetValue(BarOpacityProperty, value); }
        public double RangeMin { get => (double)GetValue(RangeMinProperty); set => SetValue(RangeMinProperty, value); }
        public double RangeMax { get => (double)GetValue(RangeMaxProperty); set => SetValue(RangeMaxProperty, value); }

        // Binning 결과 캐시
        private double _binMin, _binMax, _binWidth;
        private int _binCount;
        private int[][] _seriesCounts;          // [seriesIdx][binIdx]
        private int _maxCount;
        private bool _binningDirty = true;
        private long _seriesArrayLengthsHash;

        private static void OnBinningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Histogram h) h._binningDirty = true;
        }

        protected override void ComputeDataRange(out double minX, out double maxX, out double minY, out double maxY)
        {
            EnsureBinned();
            if (_binCount == 0)
            {
                minX = 0; maxX = 1; minY = 0; maxY = 1;
                return;
            }
            minX = _binMin;
            maxX = _binMax;
            minY = 0;
            maxY = Math.Max(1, _maxCount);
        }

        protected override void DrawPlot(DrawingContext dc)
        {
            EnsureBinned();
            var r = PlotRect;
            if (r.Width <= 0 || r.Height <= 0 || _binCount == 0) return;
            var series = Series;
            if (series == null) return;

            dc.PushClip(new RectangleGeometry(r));
            try
            {
                var alpha = (byte)Math.Max(0, Math.Min(255, BarOpacity * 255));
                for (var sIdx = 0; sIdx < series.Count; sIdx++)
                {
                    var ser = series[sIdx];
                    if (ser == null || !ser.IsVisible) continue;
                    if (sIdx >= _seriesCounts.Length) continue;
                    var counts = _seriesCounts[sIdx];
                    if (counts == null) continue;

                    var color = (ser.Brush as SolidColorBrush)?.Color ?? Colors.SteelBlue;
                    var fill = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
                    fill.Freeze();
                    var stroke = new SolidColorBrush(color); stroke.Freeze();
                    var pen = new Pen(stroke, 1.0); pen.Freeze();

                    var baselineY = DataToViewY(0);
                    for (var b = 0; b < _binCount; b++)
                    {
                        var c = counts[b];
                        if (c == 0) continue;
                        var x0 = _binMin + b * _binWidth;
                        var x1 = x0 + _binWidth;
                        var px0 = DataToViewX(x0);
                        var px1 = DataToViewX(x1);
                        var py = DataToViewY(c);
                        var rect = new Rect(Math.Min(px0, px1), Math.Min(py, baselineY),
                                            Math.Abs(px1 - px0), Math.Abs(baselineY - py));
                        if (rect.Width > 0 && rect.Height > 0)
                            dc.DrawRectangle(fill, pen, rect);
                    }
                }
            }
            finally { dc.Pop(); }
        }

        private void EnsureBinned()
        {
            var hash = ComputeSeriesArrayLengthsHash();
            if (!_binningDirty && hash == _seriesArrayLengthsHash) return;
            _binningDirty = false;
            _seriesArrayLengthsHash = hash;

            var series = Series;
            if (series == null || series.Count == 0)
            {
                _binCount = 0;
                _seriesCounts = Array.Empty<int[]>();
                _maxCount = 0;
                return;
            }

            // 1) Range 결정
            double minV = double.IsFinite(RangeMin) ? RangeMin : double.PositiveInfinity;
            double maxV = double.IsFinite(RangeMax) ? RangeMax : double.NegativeInfinity;
            if (!double.IsFinite(RangeMin) || !double.IsFinite(RangeMax))
            {
                foreach (var s in series)
                {
                    if (s == null || !s.IsVisible) continue;
                    s.GetArrays(out _, out var ys, out var n);
                    for (var i = 0; i < n; i++)
                    {
                        var v = ys[i];
                        if (!double.IsFinite(v)) continue;
                        if (v < minV) minV = v;
                        if (v > maxV) maxV = v;
                    }
                }
            }
            if (!double.IsFinite(minV) || !double.IsFinite(maxV) || maxV <= minV)
            {
                _binCount = 0;
                _seriesCounts = Array.Empty<int[]>();
                _maxCount = 0;
                return;
            }

            // 2) BinCount/BinSize 결정
            int nBins;
            double binWidth;
            if (double.IsFinite(BinSize) && BinSize > 0)
            {
                binWidth = BinSize;
                nBins = (int)Math.Ceiling((maxV - minV) / binWidth);
                if (nBins < 1) nBins = 1;
            }
            else
            {
                nBins = Math.Max(1, BinCount);
                binWidth = (maxV - minV) / nBins;
            }

            _binMin = minV;
            _binMax = minV + nBins * binWidth;
            _binWidth = binWidth;
            _binCount = nBins;

            // 3) Counting
            _seriesCounts = new int[series.Count][];
            _maxCount = 0;
            for (var sIdx = 0; sIdx < series.Count; sIdx++)
            {
                var s = series[sIdx];
                if (s == null) { _seriesCounts[sIdx] = null; continue; }
                s.GetArrays(out _, out var ys, out var n);
                var counts = new int[nBins];
                for (var i = 0; i < n; i++)
                {
                    var v = ys[i];
                    if (!double.IsFinite(v) || v < _binMin || v > _binMax) continue;
                    var b = (int)((v - _binMin) / binWidth);
                    if (b >= nBins) b = nBins - 1;
                    counts[b]++;
                    if (counts[b] > _maxCount) _maxCount = counts[b];
                }
                _seriesCounts[sIdx] = counts;
            }
        }

        private long ComputeSeriesArrayLengthsHash()
        {
            unchecked
            {
                var h = 17L;
                if (Series != null)
                {
                    foreach (var s in Series)
                    {
                        if (s == null) continue;
                        h = h * 31 + s.Count;
                        h = h * 31 + (s.IsVisible ? 1 : 0);
                    }
                }
                return h;
            }
        }

        protected override ChartHoverState HitTestHover(Point screenPx)
        {
            EnsureBinned();
            var r = PlotRect;
            if (!r.Contains(screenPx) || _binCount == 0) return ChartHoverState.Empty;
            var dataX = ViewToDataX(screenPx.X);
            var b = (int)((dataX - _binMin) / _binWidth);
            if (b < 0 || b >= _binCount) return ChartHoverState.Empty;
            // 가장 큰 count 시리즈 picking
            var bestSer = -1;
            var bestCount = -1;
            for (var sIdx = 0; sIdx < (_seriesCounts?.Length ?? 0); sIdx++)
            {
                var counts = _seriesCounts[sIdx];
                if (counts == null) continue;
                var c = counts[b];
                if (c > bestCount) { bestCount = c; bestSer = sIdx; }
            }
            if (bestSer < 0 || bestCount <= 0) return ChartHoverState.Empty;
            var binStart = _binMin + b * _binWidth;
            var binEnd = binStart + _binWidth;
            var title = Series?[bestSer]?.Title;
            var tag = $"[{binStart:0.##}, {binEnd:0.##})";
            return new ChartHoverState(bestSer, b, (binStart + binEnd) / 2, bestCount, screenPx, title, tag);
        }
    }
}
