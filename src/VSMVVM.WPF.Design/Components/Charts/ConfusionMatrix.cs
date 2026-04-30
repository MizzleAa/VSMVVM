using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts
{
    public class ConfusionMatrix : ZoomableElement
    {
        public static readonly DependencyProperty MatrixProperty =
            DependencyProperty.Register(nameof(Matrix), typeof(double[,]), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RowLabelsProperty =
            DependencyProperty.Register(nameof(RowLabels), typeof(IList<string>), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ColumnLabelsProperty =
            DependencyProperty.Register(nameof(ColumnLabels), typeof(IList<string>), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RowAxisTitleProperty =
            DependencyProperty.Register(nameof(RowAxisTitle), typeof(string), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata("Actual", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ColumnAxisTitleProperty =
            DependencyProperty.Register(nameof(ColumnAxisTitle), typeof(string), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata("Predicted", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LowColorBrushProperty =
            DependencyProperty.Register(nameof(LowColorBrush), typeof(Brush), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HighColorBrushProperty =
            DependencyProperty.Register(nameof(HighColorBrush), typeof(Brush), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TextBrushProperty =
            DependencyProperty.Register(nameof(TextBrush), typeof(Brush), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AxisBrushProperty =
            DependencyProperty.Register(nameof(AxisBrush), typeof(Brush), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowCellValuesProperty =
            DependencyProperty.Register(nameof(ShowCellValues), typeof(bool), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PlotMarginProperty =
            DependencyProperty.Register(nameof(PlotMargin), typeof(Thickness), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(new Thickness(80, 24, 80, 60), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ColorMapProperty =
            DependencyProperty.Register(nameof(ColorMap), typeof(ColorMap), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(ColorMap.Viridis, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowColorBarProperty =
            DependencyProperty.Register(nameof(ShowColorBar), typeof(bool), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CellValueFontSizeProperty =
            DependencyProperty.Register(nameof(CellValueFontSize), typeof(double), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(11.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelFontSizeProperty =
            DependencyProperty.Register(nameof(LabelFontSize), typeof(double), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(11.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AxisTitleFontSizeProperty =
            DependencyProperty.Register(nameof(AxisTitleFontSize), typeof(double), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(new FontFamily("Segoe UI"), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(ConfusionMatrix),
                new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsRender));

        public double[,] Matrix { get => (double[,])GetValue(MatrixProperty); set => SetValue(MatrixProperty, value); }
        public IList<string> RowLabels { get => (IList<string>)GetValue(RowLabelsProperty); set => SetValue(RowLabelsProperty, value); }
        public IList<string> ColumnLabels { get => (IList<string>)GetValue(ColumnLabelsProperty); set => SetValue(ColumnLabelsProperty, value); }
        public string RowAxisTitle { get => (string)GetValue(RowAxisTitleProperty); set => SetValue(RowAxisTitleProperty, value); }
        public string ColumnAxisTitle { get => (string)GetValue(ColumnAxisTitleProperty); set => SetValue(ColumnAxisTitleProperty, value); }
        public Brush LowColorBrush { get => (Brush)GetValue(LowColorBrushProperty); set => SetValue(LowColorBrushProperty, value); }
        public Brush HighColorBrush { get => (Brush)GetValue(HighColorBrushProperty); set => SetValue(HighColorBrushProperty, value); }
        public Brush TextBrush { get => (Brush)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
        public Brush AxisBrush { get => (Brush)GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
        public bool ShowCellValues { get => (bool)GetValue(ShowCellValuesProperty); set => SetValue(ShowCellValuesProperty, value); }
        public Thickness PlotMargin { get => (Thickness)GetValue(PlotMarginProperty); set => SetValue(PlotMarginProperty, value); }
        public ColorMap ColorMap { get => (ColorMap)GetValue(ColorMapProperty); set => SetValue(ColorMapProperty, value); }
        public bool ShowColorBar { get => (bool)GetValue(ShowColorBarProperty); set => SetValue(ShowColorBarProperty, value); }
        public double CellValueFontSize { get => (double)GetValue(CellValueFontSizeProperty); set => SetValue(CellValueFontSizeProperty, value); }
        public double LabelFontSize { get => (double)GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }
        public double AxisTitleFontSize { get => (double)GetValue(AxisTitleFontSizeProperty); set => SetValue(AxisTitleFontSizeProperty, value); }
        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

        public event EventHandler<ConfusionMatrixHoverState> HoverChanged;

        private readonly FormattedTextCache _textCache = new();

        public ConfusionMatrix()
        {
            ClipToBounds = true;
            Loaded += (s, e) =>
            {
                try { _textCache.SetPixelsPerDip(VisualTreeHelper.GetDpi(this).PixelsPerDip); }
                catch { _textCache.SetPixelsPerDip(1.0); }
            };
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var matrix = Matrix;
            if (matrix == null) return;
            var rows = matrix.GetLength(0);
            var cols = matrix.GetLength(1);
            if (rows == 0 || cols == 0) return;

            var w = Math.Max(0, ActualWidth - PlotMargin.Left - PlotMargin.Right);
            var h = Math.Max(0, ActualHeight - PlotMargin.Top - PlotMargin.Bottom);
            if (w <= 0 || h <= 0) return;
            var plotRect = new Rect(PlotMargin.Left, PlotMargin.Top, w, h);

            var cellW = w / cols;
            var cellH = h / rows;

            var maxVal = 0.0;
            for (var i = 0; i < rows; i++)
                for (var j = 0; j < cols; j++)
                    if (double.IsFinite(matrix[i, j]) && matrix[i, j] > maxVal) maxVal = matrix[i, j];
            if (maxVal <= 0) maxVal = 1;

            var lowColor = (LowColorBrush as SolidColorBrush)?.Color ?? Color.FromRgb(24, 24, 27);
            var highColor = (HighColorBrush as SolidColorBrush)?.Color ?? Color.FromRgb(59, 130, 246);
            var textBrush = TextBrush ?? Brushes.Gainsboro;
            var axisBrush = AxisBrush ?? Brushes.DimGray;
            var axisPen = new Pen(axisBrush, 1.0); axisPen.Freeze();

            var colorMap = ColorMap;
            var cellFontSize = CellValueFontSize;
            var fontFamily = FontFamily;
            var fontWeight = FontWeight;

            // Cell 그리기는 PushTransform 안에서 — 줌/팬 영향 받음
            dc.PushClip(new RectangleGeometry(plotRect));
            dc.PushTransform(CurrentTransform);
            try
            {
                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        var v = matrix[i, j];
                        var t = double.IsFinite(v) ? Math.Max(0, Math.Min(1, v / maxVal)) : 0;
                        var c = colorMap == ColorMap.Custom
                            ? ColorMaps.SampleCustom(lowColor, highColor, t)
                            : ColorMaps.Sample(colorMap, t);
                        var cellBrush = new SolidColorBrush(c); cellBrush.Freeze();
                        var rect = new Rect(plotRect.Left + j * cellW, plotRect.Top + i * cellH, cellW, cellH);
                        dc.DrawRectangle(cellBrush, axisPen, rect);

                        if (ShowCellValues && cellW >= 24 && cellH >= 18)
                        {
                            var labelBrush = t > 0.55 ? Brushes.White : textBrush;
                            var ft = _textCache.Get(FormatCellValue(v), cellFontSize, labelBrush, fontFamily, fontWeight);
                            if (ft.Width <= cellW - 4 && ft.Height <= cellH - 4)
                                dc.DrawText(ft, new Point(rect.Left + (cellW - ft.Width) / 2, rect.Top + (cellH - ft.Height) / 2));
                        }
                    }
                }
            }
            finally { dc.Pop(); dc.Pop(); }

            // 라벨·타이틀은 PushTransform 밖 (줌/팬 영향 안 받음)
            var labelFontSize = LabelFontSize;
            var titleFontSize = AxisTitleFontSize;

            var colLabels = ColumnLabels;
            if (colLabels != null)
            {
                for (var j = 0; j < cols && j < colLabels.Count; j++)
                {
                    var label = colLabels[j] ?? string.Empty;
                    var ft = _textCache.Get(label, labelFontSize, textBrush, fontFamily, fontWeight);
                    // transform 적용된 cell 위치 기준 — 라벨도 같이 이동
                    var dataCenterX = plotRect.Left + (j + 0.5) * cellW;
                    var screenCenterX = dataCenterX * Scale + TranslateX;
                    if (screenCenterX < plotRect.Left - 0.5 || screenCenterX > plotRect.Right + 0.5) continue;
                    if (ft.Width <= cellW * Scale - 4 || cellW * Scale < 12)
                        dc.DrawText(ft, new Point(screenCenterX - ft.Width / 2, plotRect.Top - ft.Height - 4));
                }
            }
            var rowLabels = RowLabels;
            if (rowLabels != null)
            {
                for (var i = 0; i < rows && i < rowLabels.Count; i++)
                {
                    var label = rowLabels[i] ?? string.Empty;
                    var ft = _textCache.Get(label, labelFontSize, textBrush, fontFamily, fontWeight);
                    var dataCenterY = plotRect.Top + (i + 0.5) * cellH;
                    var screenCenterY = dataCenterY * Scale + TranslateY;
                    if (screenCenterY < plotRect.Top - 0.5 || screenCenterY > plotRect.Bottom + 0.5) continue;
                    dc.DrawText(ft, new Point(plotRect.Left - ft.Width - 6, screenCenterY - ft.Height / 2));
                }
            }

            if (!string.IsNullOrEmpty(ColumnAxisTitle))
            {
                var ft = _textCache.Get(ColumnAxisTitle, titleFontSize, textBrush, fontFamily, fontWeight);
                dc.DrawText(ft, new Point(plotRect.Left + (plotRect.Width - ft.Width) / 2, ActualHeight - ft.Height - 4));
            }
            if (!string.IsNullOrEmpty(RowAxisTitle))
            {
                var ft = _textCache.Get(RowAxisTitle, titleFontSize, textBrush, fontFamily, fontWeight);
                var pivotX = 14.0;
                var pivotY = plotRect.Top + plotRect.Height / 2;
                dc.PushTransform(new RotateTransform(-90, pivotX, pivotY));
                dc.DrawText(ft, new Point(pivotX - ft.Width / 2, pivotY - ft.Height / 2));
                dc.Pop();
            }

            if (ShowColorBar)
            {
                var grad = colorMap == ColorMap.Custom
                    ? ColorMaps.CreateCustomGradient(lowColor, highColor, vertical: true)
                    : ColorMaps.CreateGradientBrush(colorMap, vertical: true);
                ColorBarRenderer.Draw(dc, plotRect, ActualWidth, grad, 0, maxVal, textBrush, _textCache);
            }
        }

        private static string FormatCellValue(double v)
        {
            if (!double.IsFinite(v)) return string.Empty;
            if (v == Math.Floor(v) && Math.Abs(v) < 1e6) return ((long)v).ToString();
            return v.ToString("0.##");
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (IsPanning) return;
            var matrix = Matrix;
            if (matrix == null) return;
            var rows = matrix.GetLength(0);
            var cols = matrix.GetLength(1);
            if (rows == 0 || cols == 0) return;
            var w = Math.Max(0, ActualWidth - PlotMargin.Left - PlotMargin.Right);
            var h = Math.Max(0, ActualHeight - PlotMargin.Top - PlotMargin.Bottom);
            if (w <= 0 || h <= 0) return;
            var pos = e.GetPosition(this);
            // transform 역변환 — pan/zoom 후에도 정확한 셀 hit-test
            var content = ScreenToContent(pos);
            if (content.X < PlotMargin.Left || content.X > PlotMargin.Left + w ||
                content.Y < PlotMargin.Top || content.Y > PlotMargin.Top + h)
            {
                HoverChanged?.Invoke(this, new ConfusionMatrixHoverState());
                return;
            }
            var col = (int)((content.X - PlotMargin.Left) / (w / cols));
            var row = (int)((content.Y - PlotMargin.Top) / (h / rows));
            if (row < 0 || row >= rows || col < 0 || col >= cols) return;
            var rl = (RowLabels != null && row < RowLabels.Count) ? RowLabels[row] : row.ToString();
            var cl = (ColumnLabels != null && col < ColumnLabels.Count) ? ColumnLabels[col] : col.ToString();
            HoverChanged?.Invoke(this, new ConfusionMatrixHoverState(true, row, col, matrix[row, col], rl, cl, pos));
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            HoverChanged?.Invoke(this, new ConfusionMatrixHoverState());
        }
    }

    public readonly struct ConfusionMatrixHoverState
    {
        public bool HasValue { get; }
        public int Row { get; }
        public int Column { get; }
        public double Value { get; }
        public string RowLabel { get; }
        public string ColumnLabel { get; }
        public Point ScreenPoint { get; }

        public ConfusionMatrixHoverState(bool hasValue, int row, int col, double value, string rowLabel, string colLabel, Point screen)
        {
            HasValue = hasValue; Row = row; Column = col; Value = value;
            RowLabel = rowLabel; ColumnLabel = colLabel; ScreenPoint = screen;
        }
    }
}
