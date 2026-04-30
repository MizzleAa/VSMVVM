using System;
using System.Windows;
using System.Windows.Media;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts
{
    public class Heatmap : ChartBase
    {
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(nameof(Data), typeof(double[,]), typeof(Heatmap),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

        public static readonly DependencyProperty XMinProperty =
            DependencyProperty.Register(nameof(XMin), typeof(double), typeof(Heatmap),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnRangeChanged));

        public static readonly DependencyProperty XMaxProperty =
            DependencyProperty.Register(nameof(XMax), typeof(double), typeof(Heatmap),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnRangeChanged));

        public static readonly DependencyProperty YMinProperty =
            DependencyProperty.Register(nameof(YMin), typeof(double), typeof(Heatmap),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnRangeChanged));

        public static readonly DependencyProperty YMaxProperty =
            DependencyProperty.Register(nameof(YMax), typeof(double), typeof(Heatmap),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnRangeChanged));

        public static readonly DependencyProperty ColorMapProperty =
            DependencyProperty.Register(nameof(ColorMap), typeof(ColorMap), typeof(Heatmap),
                new FrameworkPropertyMetadata(ColorMap.Viridis, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LowColorBrushProperty =
            DependencyProperty.Register(nameof(LowColorBrush), typeof(Brush), typeof(Heatmap),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HighColorBrushProperty =
            DependencyProperty.Register(nameof(HighColorBrush), typeof(Brush), typeof(Heatmap),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(Heatmap),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(Heatmap),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowColorBarProperty =
            DependencyProperty.Register(nameof(ShowColorBar), typeof(bool), typeof(Heatmap),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public double[,] Data { get => (double[,])GetValue(DataProperty); set => SetValue(DataProperty, value); }
        public double XMin { get => (double)GetValue(XMinProperty); set => SetValue(XMinProperty, value); }
        public double XMax { get => (double)GetValue(XMaxProperty); set => SetValue(XMaxProperty, value); }
        public double YMin { get => (double)GetValue(YMinProperty); set => SetValue(YMinProperty, value); }
        public double YMax { get => (double)GetValue(YMaxProperty); set => SetValue(YMaxProperty, value); }
        public ColorMap ColorMap { get => (ColorMap)GetValue(ColorMapProperty); set => SetValue(ColorMapProperty, value); }
        public Brush LowColorBrush { get => (Brush)GetValue(LowColorBrushProperty); set => SetValue(LowColorBrushProperty, value); }
        public Brush HighColorBrush { get => (Brush)GetValue(HighColorBrushProperty); set => SetValue(HighColorBrushProperty, value); }
        public double MinValue { get => (double)GetValue(MinValueProperty); set => SetValue(MinValueProperty, value); }
        public double MaxValue { get => (double)GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }
        public bool ShowColorBar { get => (bool)GetValue(ShowColorBarProperty); set => SetValue(ShowColorBarProperty, value); }

        public Heatmap()
        {
            PlotMargin = new Thickness(56, 12, 80, 44);
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Heatmap h) h.DataRangeDirty = true;
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Heatmap h) h.DataRangeDirty = true;
        }

        protected override void ComputeDataRange(out double minX, out double maxX, out double minY, out double maxY)
        {
            minX = XMin; maxX = XMax; minY = YMin; maxY = YMax;
            if (!double.IsFinite(minX) || !double.IsFinite(maxX) || maxX <= minX) { minX = 0; maxX = 1; }
            if (!double.IsFinite(minY) || !double.IsFinite(maxY) || maxY <= minY) { minY = 0; maxY = 1; }
        }

        protected override void DrawPlot(DrawingContext dc)
        {
            var data = Data;
            if (data == null) return;
            var rows = data.GetLength(0);
            var cols = data.GetLength(1);
            if (rows == 0 || cols == 0) return;

            var r = PlotRect;
            if (r.Width <= 0 || r.Height <= 0) return;

            // Min/Max 결정
            var minV = MinValue;
            var maxV = MaxValue;
            if (!double.IsFinite(minV) || !double.IsFinite(maxV))
            {
                var dataMin = double.PositiveInfinity;
                var dataMax = double.NegativeInfinity;
                for (var i = 0; i < rows; i++)
                    for (var j = 0; j < cols; j++)
                    {
                        var v = data[i, j];
                        if (!double.IsFinite(v)) continue;
                        if (v < dataMin) dataMin = v;
                        if (v > dataMax) dataMax = v;
                    }
                if (!double.IsFinite(minV)) minV = dataMin;
                if (!double.IsFinite(maxV)) maxV = dataMax;
            }
            if (maxV <= minV) maxV = minV + 1;

            var dataRangeX = XMax - XMin;
            var dataRangeY = YMax - YMin;
            if (dataRangeX <= 0 || dataRangeY <= 0) return;

            var cellDataW = dataRangeX / cols;
            var cellDataH = dataRangeY / rows;

            // Viewport에 보이는 셀 인덱스 범위
            var visMinX = Math.Max(XMin, ViewMinX);
            var visMaxX = Math.Min(XMax, ViewMaxX);
            var visMinY = Math.Max(YMin, ViewMinY);
            var visMaxY = Math.Min(YMax, ViewMaxY);
            if (visMaxX <= visMinX || visMaxY <= visMinY) return;

            var c0 = Math.Max(0, (int)Math.Floor((visMinX - XMin) / cellDataW));
            var c1 = Math.Min(cols, (int)Math.Ceiling((visMaxX - XMin) / cellDataW));
            var r0 = Math.Max(0, (int)Math.Floor((visMinY - YMin) / cellDataH));
            var r1 = Math.Min(rows, (int)Math.Ceiling((visMaxY - YMin) / cellDataH));

            var lowColor = (LowColorBrush as SolidColorBrush)?.Color ?? Color.FromRgb(24, 24, 27);
            var highColor = (HighColorBrush as SolidColorBrush)?.Color ?? Color.FromRgb(59, 130, 246);
            var colorMap = ColorMap;

            dc.PushClip(new RectangleGeometry(r));
            try
            {
                for (var i = r0; i < r1; i++)
                {
                    for (var j = c0; j < c1; j++)
                    {
                        var v = data[i, j];
                        if (!double.IsFinite(v)) continue;
                        var t = (v - minV) / (maxV - minV);
                        if (t < 0) t = 0; else if (t > 1) t = 1;
                        var color = colorMap == ColorMap.Custom
                            ? ColorMaps.SampleCustom(lowColor, highColor, t)
                            : ColorMaps.Sample(colorMap, t);
                        var brush = new SolidColorBrush(color); brush.Freeze();

                        var dxL = XMin + j * cellDataW;
                        var dxR = dxL + cellDataW;
                        var dyB = YMin + i * cellDataH;
                        var dyT = dyB + cellDataH;
                        var pxL = DataToViewX(dxL);
                        var pxR = DataToViewX(dxR);
                        var pyT = DataToViewY(dyT);
                        var pyB = DataToViewY(dyB);
                        var w = pxR - pxL;
                        var h = pyB - pyT;
                        if (w < 0.5 || h < 0.5) continue;
                        dc.DrawRectangle(brush, null, new Rect(pxL, pyT, w, h));
                    }
                }
            }
            finally { dc.Pop(); }

            if (ShowColorBar)
            {
                var grad = colorMap == ColorMap.Custom
                    ? ColorMaps.CreateCustomGradient(lowColor, highColor, vertical: true)
                    : ColorMaps.CreateGradientBrush(colorMap, vertical: true);
                var textBrush = TextBrush ?? Brushes.Gray;
                ColorBarRenderer.Draw(dc, r, ActualWidth, grad, minV, maxV, textBrush, TextCache);
            }
        }

        protected override ChartHoverState HitTestHover(Point screenPx)
        {
            var data = Data;
            if (data == null) return ChartHoverState.Empty;
            var rows = data.GetLength(0);
            var cols = data.GetLength(1);
            if (rows == 0 || cols == 0) return ChartHoverState.Empty;
            var r = PlotRect;
            if (!r.Contains(screenPx)) return ChartHoverState.Empty;

            var dx = ViewToDataX(screenPx.X);
            var dy = ViewToDataY(screenPx.Y);
            if (dx < XMin || dx > XMax || dy < YMin || dy > YMax) return ChartHoverState.Empty;

            var col = (int)((dx - XMin) / (XMax - XMin) * cols);
            var row = (int)((dy - YMin) / (YMax - YMin) * rows);
            if (col < 0 || col >= cols || row < 0 || row >= rows) return ChartHoverState.Empty;

            var v = data[row, col];
            return new ChartHoverState(0, row * cols + col, dx, v, screenPx, $"[{row},{col}]");
        }
    }
}
