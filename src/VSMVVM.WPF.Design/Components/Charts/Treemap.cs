using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts
{
    public class TreemapItem
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public Brush Brush { get; set; }
        public object Tag { get; set; }
    }

    public readonly struct TreemapHoverState
    {
        public bool HasValue { get; }
        public int Index { get; }
        public string Label { get; }
        public double Value { get; }
        public double Percentage { get; }
        public Point ScreenPoint { get; }

        public TreemapHoverState(int index, string label, double value, double percentage, Point screenPoint)
        {
            HasValue = true;
            Index = index;
            Label = label;
            Value = value;
            Percentage = percentage;
            ScreenPoint = screenPoint;
        }

        public static TreemapHoverState Empty => default;
    }

    /// <summary>
    /// Squarified treemap (Matplotlib squarify 호환).
    /// 항목 면적이 값에 비례하고 가급적 정사각형에 가깝게 배치된다.
    /// </summary>
    public class Treemap : ZoomableElement
    {
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(IList<TreemapItem>), typeof(Treemap),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsChanged));

        public static readonly DependencyProperty ColorMapProperty =
            DependencyProperty.Register(nameof(ColorMap), typeof(ColorMap), typeof(Treemap),
                new FrameworkPropertyMetadata(ColorMap.Viridis, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UseColorMapProperty =
            DependencyProperty.Register(nameof(UseColorMap), typeof(bool), typeof(Treemap),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BorderBrushProperty =
            DependencyProperty.Register(nameof(BorderBrush), typeof(Brush), typeof(Treemap),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TextBrushProperty =
            DependencyProperty.Register(nameof(TextBrush), typeof(Brush), typeof(Treemap),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PaddingProperty =
            DependencyProperty.Register(nameof(Padding), typeof(double), typeof(Treemap),
                new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowValuesProperty =
            DependencyProperty.Register(nameof(ShowValues), typeof(bool), typeof(Treemap),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelFontSizeProperty =
            DependencyProperty.Register(nameof(LabelFontSize), typeof(double), typeof(Treemap),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ValueFontSizeProperty =
            DependencyProperty.Register(nameof(ValueFontSize), typeof(double), typeof(Treemap),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(Treemap),
                new FrameworkPropertyMetadata(new FontFamily("Segoe UI"), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(Treemap),
                new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsRender));

        public IList<TreemapItem> Items { get => (IList<TreemapItem>)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
        public ColorMap ColorMap { get => (ColorMap)GetValue(ColorMapProperty); set => SetValue(ColorMapProperty, value); }
        public bool UseColorMap { get => (bool)GetValue(UseColorMapProperty); set => SetValue(UseColorMapProperty, value); }
        public Brush BorderBrush { get => (Brush)GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
        public Brush TextBrush { get => (Brush)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
        public double Padding { get => (double)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
        public bool ShowValues { get => (bool)GetValue(ShowValuesProperty); set => SetValue(ShowValuesProperty, value); }
        public double LabelFontSize { get => (double)GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }
        public double ValueFontSize { get => (double)GetValue(ValueFontSizeProperty); set => SetValue(ValueFontSizeProperty, value); }
        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

        public event EventHandler<TreemapHoverState> HoverChanged;

        private readonly FormattedTextCache _textCache = new();
        private readonly List<(int index, Rect rect)> _layoutCache = new();
        private double _totalValue;

        public Treemap()
        {
            ClipToBounds = true;
            Loaded += (s, e) =>
            {
                try { _textCache.SetPixelsPerDip(VisualTreeHelper.GetDpi(this).PixelsPerDip); }
                catch { _textCache.SetPixelsPerDip(1.0); }
            };
        }

        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Treemap t) t._layoutCache.Clear();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var items = Items;
            if (items == null || items.Count == 0) return;
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            // Sort indices by value desc
            var indexed = new List<(int idx, double value)>(items.Count);
            _totalValue = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var v = items[i]?.Value ?? 0;
                if (!double.IsFinite(v) || v <= 0) continue;
                indexed.Add((i, v));
                _totalValue += v;
            }
            if (_totalValue <= 0 || indexed.Count == 0) return;
            indexed.Sort((a, b) => b.value.CompareTo(a.value));

            var values = indexed.Select(x => x.value).ToList();
            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            var rects = Squarify(values, _totalValue, bounds);

            _layoutCache.Clear();
            var maxValue = values[0];
            var minValue = values[values.Count - 1];
            var valueRange = Math.Max(1e-12, maxValue - minValue);

            var borderBrush = BorderBrush ?? Brushes.Black;
            var textBrush = TextBrush ?? Brushes.White;
            var borderPen = new Pen(borderBrush, 1.0); borderPen.Freeze();
            var pad = Padding;
            var labelSize = LabelFontSize;
            var valueSize = ValueFontSize;
            var fontFamily = FontFamily;
            var fontWeight = FontWeight;

            // Treemap 전체 cell을 zoom/pan transform 안에서 그림
            dc.PushTransform(CurrentTransform);
            try
            {
                for (var k = 0; k < rects.Count; k++)
                {
                    var origIdx = indexed[k].idx;
                    var item = items[origIdx];
                    var rect = rects[k];
                    _layoutCache.Add((origIdx, rect));

                    var inner = pad > 0
                        ? new Rect(rect.X + pad, rect.Y + pad,
                                   Math.Max(0, rect.Width - pad * 2), Math.Max(0, rect.Height - pad * 2))
                        : rect;
                    if (inner.Width <= 0 || inner.Height <= 0) continue;

                    Brush fill;
                    if (UseColorMap && ColorMap != ColorMap.Custom)
                    {
                        var t = (item.Value - minValue) / valueRange;
                        var color = ColorMaps.Sample(ColorMap, t);
                        var sb = new SolidColorBrush(color); sb.Freeze();
                        fill = sb;
                    }
                    else
                    {
                        fill = item.Brush ?? Brushes.SteelBlue;
                    }

                    dc.DrawRectangle(fill, borderPen, inner);

                    // 텍스트는 Scale 적용된 효과적 크기 기준으로 가시성 판단
                    var effectiveW = inner.Width * Scale;
                    var effectiveH = inner.Height * Scale;
                    if (effectiveW >= 40 && effectiveH >= 18)
                    {
                        var labelText = item.Label ?? string.Empty;
                        var labelBrush = ChooseTextBrushFor(fill, textBrush);
                        var labelFt = _textCache.Get(labelText, labelSize, labelBrush, fontFamily, fontWeight);
                        if (labelFt.Width <= effectiveW - 6 && labelFt.Height <= effectiveH - 6)
                        {
                            // 텍스트 위치를 데이터 좌표로 두면 Scale에 따라 글자 크기도 함께 변함 (원하는 동작 — zoom in 시 글자 커짐)
                            // 글자 크기 자체는 일정하게 유지하고 위치만 따라가게 하려면 PushTransform 밖에서 그려야 하는데,
                            // 이 경우 zoom in 효과가 줄어들어 zoom 의미가 약해짐. 일단 transform 안에서 그리기로 결정.
                            dc.DrawText(labelFt, new Point(inner.X + 4 / Scale, inner.Y + 4 / Scale));
                        }
                        if (ShowValues && effectiveH >= 36)
                        {
                            var valFt = _textCache.Get(FormatValue(item.Value), valueSize, labelBrush, fontFamily, fontWeight);
                            if (valFt.Width <= effectiveW - 6)
                                dc.DrawText(valFt, new Point(inner.X + 4 / Scale,
                                                              inner.Y + 4 / Scale + labelFt.Height / Scale + 2 / Scale));
                        }
                    }
                }
            }
            finally { dc.Pop(); }
        }

        private static Brush ChooseTextBrushFor(Brush fill, Brush fallback)
        {
            if (fill is SolidColorBrush scb)
            {
                var c = scb.Color;
                // Relative luminance (sRGB approximation)
                var lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
                return lum > 0.55 ? Brushes.Black : Brushes.White;
            }
            return fallback;
        }

        private static string FormatValue(double v)
        {
            if (v >= 1e6) return (v / 1e6).ToString("0.#") + "M";
            if (v >= 1e3) return (v / 1e3).ToString("0.#") + "k";
            if (v == Math.Floor(v) && Math.Abs(v) < 1e9) return ((long)v).ToString();
            return v.ToString("0.##");
        }

        // Squarified treemap layout (Bruls et al. 2000)
        private static List<Rect> Squarify(IList<double> values, double totalValue, Rect bounds)
        {
            var areaRatio = bounds.Width * bounds.Height / totalValue;
            var areas = values.Select(v => v * areaRatio).ToList();
            var result = new List<Rect>(values.Count);
            SquarifyImpl(areas, 0, areas.Count, bounds, result);
            return result;
        }

        private static void SquarifyImpl(List<double> areas, int start, int end, Rect bounds, List<Rect> result)
        {
            if (start >= end) return;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                for (var i = start; i < end; i++) result.Add(Rect.Empty);
                return;
            }

            var shortSide = Math.Min(bounds.Width, bounds.Height);
            var rowEnd = start;
            var rowSum = 0.0;
            var bestWorst = double.PositiveInfinity;

            // 한 행에 포함시킬 항목 수 결정
            for (var i = start; i < end; i++)
            {
                var newSum = rowSum + areas[i];
                var worst = Worst(areas, start, i + 1, newSum, shortSide);
                if (worst > bestWorst)
                {
                    rowEnd = i;
                    break;
                }
                bestWorst = worst;
                rowSum = newSum;
                rowEnd = i + 1;
            }
            if (rowEnd == start) rowEnd = start + 1;

            // 행 배치
            var rowAreaSum = 0.0;
            for (var i = start; i < rowEnd; i++) rowAreaSum += areas[i];
            var rowThickness = rowAreaSum / shortSide;

            Rect remaining;
            if (bounds.Width >= bounds.Height)
            {
                // 좌측에 세로 행
                var x = bounds.X;
                var y = bounds.Y;
                for (var i = start; i < rowEnd; i++)
                {
                    var h = areas[i] / rowThickness;
                    result.Add(new Rect(x, y, rowThickness, h));
                    y += h;
                }
                remaining = new Rect(bounds.X + rowThickness, bounds.Y,
                                     Math.Max(0, bounds.Width - rowThickness), bounds.Height);
            }
            else
            {
                // 상단에 가로 행
                var x = bounds.X;
                var y = bounds.Y;
                for (var i = start; i < rowEnd; i++)
                {
                    var w = areas[i] / rowThickness;
                    result.Add(new Rect(x, y, w, rowThickness));
                    x += w;
                }
                remaining = new Rect(bounds.X, bounds.Y + rowThickness,
                                     bounds.Width, Math.Max(0, bounds.Height - rowThickness));
            }

            SquarifyImpl(areas, rowEnd, end, remaining, result);
        }

        private static double Worst(List<double> areas, int start, int end, double rowSum, double shortSide)
        {
            if (rowSum <= 0) return double.PositiveInfinity;
            var s2 = shortSide * shortSide;
            var sum2 = rowSum * rowSum;
            var maxA = double.NegativeInfinity;
            var minA = double.PositiveInfinity;
            for (var i = start; i < end; i++)
            {
                if (areas[i] > maxA) maxA = areas[i];
                if (areas[i] < minA) minA = areas[i];
            }
            return Math.Max(s2 * maxA / sum2, sum2 / (s2 * minA));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (IsPanning) return;
            var pos = e.GetPosition(this);
            // hit test는 transform 역변환된 content 좌표로
            var content = ScreenToContent(pos);
            for (var k = 0; k < _layoutCache.Count; k++)
            {
                var (idx, rect) = _layoutCache[k];
                if (rect.Contains(content))
                {
                    var item = Items[idx];
                    var pct = _totalValue > 0 ? (item.Value / _totalValue * 100) : 0;
                    HoverChanged?.Invoke(this, new TreemapHoverState(idx, item.Label, item.Value, pct, pos));
                    return;
                }
            }
            HoverChanged?.Invoke(this, TreemapHoverState.Empty);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            HoverChanged?.Invoke(this, TreemapHoverState.Empty);
        }
    }
}
