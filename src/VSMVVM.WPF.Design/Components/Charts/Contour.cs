using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts
{
    public class Contour : ChartBase
    {
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(nameof(Data), typeof(double[,]), typeof(Contour),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

        public static readonly DependencyProperty XMinProperty =
            DependencyProperty.Register(nameof(XMin), typeof(double), typeof(Contour),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnRangeChanged));

        public static readonly DependencyProperty XMaxProperty =
            DependencyProperty.Register(nameof(XMax), typeof(double), typeof(Contour),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnRangeChanged));

        public static readonly DependencyProperty YMinProperty =
            DependencyProperty.Register(nameof(YMin), typeof(double), typeof(Contour),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnRangeChanged));

        public static readonly DependencyProperty YMaxProperty =
            DependencyProperty.Register(nameof(YMax), typeof(double), typeof(Contour),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnRangeChanged));

        public static readonly DependencyProperty LevelsProperty =
            DependencyProperty.Register(nameof(Levels), typeof(IList<double>), typeof(Contour),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LevelCountProperty =
            DependencyProperty.Register(nameof(LevelCount), typeof(int), typeof(Contour),
                new FrameworkPropertyMetadata(10, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ColorMapProperty =
            DependencyProperty.Register(nameof(ColorMap), typeof(ColorMap), typeof(Contour),
                new FrameworkPropertyMetadata(ColorMap.Viridis, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LineThicknessProperty =
            DependencyProperty.Register(nameof(LineThickness), typeof(double), typeof(Contour),
                new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowColorBarProperty =
            DependencyProperty.Register(nameof(ShowColorBar), typeof(bool), typeof(Contour),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public double[,] Data { get => (double[,])GetValue(DataProperty); set => SetValue(DataProperty, value); }
        public double XMin { get => (double)GetValue(XMinProperty); set => SetValue(XMinProperty, value); }
        public double XMax { get => (double)GetValue(XMaxProperty); set => SetValue(XMaxProperty, value); }
        public double YMin { get => (double)GetValue(YMinProperty); set => SetValue(YMinProperty, value); }
        public double YMax { get => (double)GetValue(YMaxProperty); set => SetValue(YMaxProperty, value); }
        public IList<double> Levels { get => (IList<double>)GetValue(LevelsProperty); set => SetValue(LevelsProperty, value); }
        public int LevelCount { get => (int)GetValue(LevelCountProperty); set => SetValue(LevelCountProperty, value); }
        public ColorMap ColorMap { get => (ColorMap)GetValue(ColorMapProperty); set => SetValue(ColorMapProperty, value); }
        public double LineThickness { get => (double)GetValue(LineThicknessProperty); set => SetValue(LineThicknessProperty, value); }
        public bool ShowColorBar { get => (bool)GetValue(ShowColorBarProperty); set => SetValue(ShowColorBarProperty, value); }

        public Contour()
        {
            PlotMargin = new Thickness(56, 12, 80, 44);
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Contour c) c.DataRangeDirty = true;
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Contour c) c.DataRangeDirty = true;
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
            if (rows < 2 || cols < 2) return;

            var r = PlotRect;
            if (r.Width <= 0 || r.Height <= 0) return;

            // 데이터 min/max
            double dataMin = double.PositiveInfinity, dataMax = double.NegativeInfinity;
            for (var i = 0; i < rows; i++)
                for (var j = 0; j < cols; j++)
                {
                    var v = data[i, j];
                    if (!double.IsFinite(v)) continue;
                    if (v < dataMin) dataMin = v;
                    if (v > dataMax) dataMax = v;
                }
            if (!double.IsFinite(dataMin) || dataMax <= dataMin) return;

            // Levels 결정
            double[] levelArr;
            var levels = Levels;
            if (levels != null && levels.Count > 0)
            {
                levelArr = new double[levels.Count];
                for (var i = 0; i < levels.Count; i++) levelArr[i] = levels[i];
            }
            else
            {
                var n = Math.Max(2, LevelCount);
                levelArr = new double[n];
                for (var i = 0; i < n; i++)
                    levelArr[i] = dataMin + (i + 1) * (dataMax - dataMin) / (n + 1);
            }

            var dataRangeX = XMax - XMin;
            var dataRangeY = YMax - YMin;
            var cellW = dataRangeX / (cols - 1);  // X 셀 폭 (data)
            var cellH = dataRangeY / (rows - 1);  // Y 셀 높이 (data)
            var colorMap = ColorMap;

            dc.PushClip(new RectangleGeometry(r));
            try
            {
                for (var li = 0; li < levelArr.Length; li++)
                {
                    var level = levelArr[li];
                    var t = (level - dataMin) / (dataMax - dataMin);
                    var color = colorMap == ColorMap.Custom
                        ? Color.FromRgb(150, 150, 150)
                        : ColorMaps.Sample(colorMap, t);
                    var brush = new SolidColorBrush(color); brush.Freeze();
                    var pen = new Pen(brush, LineThickness); pen.Freeze();

                    var sg = new StreamGeometry();
                    using (var ctx = sg.Open())
                    {
                        for (var i = 0; i < rows - 1; i++)
                        {
                            for (var j = 0; j < cols - 1; j++)
                            {
                                var v00 = data[i, j];
                                var v01 = data[i, j + 1];
                                var v10 = data[i + 1, j];
                                var v11 = data[i + 1, j + 1];

                                var mask = 0;
                                if (v00 >= level) mask |= 1;
                                if (v01 >= level) mask |= 2;
                                if (v11 >= level) mask |= 4;
                                if (v10 >= level) mask |= 8;
                                if (mask == 0 || mask == 15) continue;

                                // 데이터 좌표 corners
                                var dxL = XMin + j * cellW;
                                var dxR = dxL + cellW;
                                var dyB = YMin + i * cellH;
                                var dyT = dyB + cellH;

                                // 변별 z=level 보간 위치 (데이터 좌표). 필요한 변만 계산.
                                Point eBottom = default, eRight = default, eTop = default, eLeft = default;
                                bool hasB = false, hasR = false, hasT = false, hasL = false;

                                if (((mask & 1) != 0) != ((mask & 2) != 0))
                                {
                                    eBottom = new Point(dxL + Frac(v00, v01, level) * cellW, dyB);
                                    hasB = true;
                                }
                                if (((mask & 2) != 0) != ((mask & 4) != 0))
                                {
                                    eRight = new Point(dxR, dyB + Frac(v01, v11, level) * cellH);
                                    hasR = true;
                                }
                                if (((mask & 8) != 0) != ((mask & 4) != 0))
                                {
                                    eTop = new Point(dxL + Frac(v10, v11, level) * cellW, dyT);
                                    hasT = true;
                                }
                                if (((mask & 1) != 0) != ((mask & 8) != 0))
                                {
                                    eLeft = new Point(dxL, dyB + Frac(v00, v10, level) * cellH);
                                    hasL = true;
                                }

                                // 데이터 → 뷰 좌표 변환
                                if (hasB) eBottom = new Point(DataToViewX(eBottom.X), DataToViewY(eBottom.Y));
                                if (hasR) eRight = new Point(DataToViewX(eRight.X), DataToViewY(eRight.Y));
                                if (hasT) eTop = new Point(DataToViewX(eTop.X), DataToViewY(eTop.Y));
                                if (hasL) eLeft = new Point(DataToViewX(eLeft.X), DataToViewY(eLeft.Y));

                                AddSegments(ctx, mask, v00, v01, v10, v11, level,
                                            eBottom, eRight, eTop, eLeft);
                            }
                        }
                    }
                    sg.Freeze();
                    dc.DrawGeometry(null, pen, sg);
                }
            }
            finally { dc.Pop(); }

            if (ShowColorBar)
            {
                var grad = colorMap == ColorMap.Custom
                    ? ColorMaps.CreateCustomGradient(Colors.Gray, Colors.White, vertical: true)
                    : ColorMaps.CreateGradientBrush(colorMap, vertical: true);
                var textBrush = TextBrush ?? Brushes.Gray;
                ColorBarRenderer.Draw(dc, r, ActualWidth, grad, dataMin, dataMax, textBrush, TextCache);
            }
        }

        private static double Frac(double va, double vb, double level)
        {
            var d = vb - va;
            if (Math.Abs(d) < 1e-12) return 0.5;
            var t = (level - va) / d;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            return t;
        }

        private static void AddSegments(StreamGeometryContext ctx, int mask,
                                         double v00, double v01, double v10, double v11, double level,
                                         Point eBottom, Point eRight, Point eTop, Point eLeft)
        {
            switch (mask)
            {
                case 1:
                case 14:
                    Segment(ctx, eLeft, eBottom); break;
                case 2:
                case 13:
                    Segment(ctx, eBottom, eRight); break;
                case 4:
                case 11:
                    Segment(ctx, eRight, eTop); break;
                case 8:
                case 7:
                    Segment(ctx, eTop, eLeft); break;
                case 3:
                case 12:
                    Segment(ctx, eLeft, eRight); break;
                case 6:
                case 9:
                    Segment(ctx, eBottom, eTop); break;
                case 5:
                {
                    var center = (v00 + v01 + v10 + v11) * 0.25;
                    if (center >= level)
                    {
                        Segment(ctx, eLeft, eTop);
                        Segment(ctx, eBottom, eRight);
                    }
                    else
                    {
                        Segment(ctx, eLeft, eBottom);
                        Segment(ctx, eTop, eRight);
                    }
                    break;
                }
                case 10:
                {
                    var center = (v00 + v01 + v10 + v11) * 0.25;
                    if (center >= level)
                    {
                        Segment(ctx, eLeft, eBottom);
                        Segment(ctx, eTop, eRight);
                    }
                    else
                    {
                        Segment(ctx, eLeft, eTop);
                        Segment(ctx, eBottom, eRight);
                    }
                    break;
                }
            }
        }

        private static void Segment(StreamGeometryContext ctx, Point a, Point b)
        {
            ctx.BeginFigure(a, false, false);
            ctx.LineTo(b, true, false);
        }
    }
}
