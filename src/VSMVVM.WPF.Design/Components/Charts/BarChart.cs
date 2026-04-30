using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts
{
    public enum BarMode { Grouped, Stacked, Overlay }

    public class BarChart : ChartBase
    {
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(BarChart),
                new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BarModeProperty =
            DependencyProperty.Register(nameof(BarMode), typeof(BarMode), typeof(BarChart),
                new FrameworkPropertyMetadata(BarMode.Grouped, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BarSpacingProperty =
            DependencyProperty.Register(nameof(BarSpacing), typeof(double), typeof(BarChart),
                new FrameworkPropertyMetadata(0.1, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CategorySpacingProperty =
            DependencyProperty.Register(nameof(CategorySpacing), typeof(double), typeof(BarChart),
                new FrameworkPropertyMetadata(0.2, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowValuesProperty =
            DependencyProperty.Register(nameof(ShowValues), typeof(bool), typeof(BarChart),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ValueFormatProperty =
            DependencyProperty.Register(nameof(ValueFormat), typeof(string), typeof(BarChart),
                new FrameworkPropertyMetadata("0.##", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ValueFontSizeProperty =
            DependencyProperty.Register(nameof(ValueFontSize), typeof(double), typeof(BarChart),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
        public BarMode BarMode { get => (BarMode)GetValue(BarModeProperty); set => SetValue(BarModeProperty, value); }
        public double BarSpacing { get => (double)GetValue(BarSpacingProperty); set => SetValue(BarSpacingProperty, value); }
        public double CategorySpacing { get => (double)GetValue(CategorySpacingProperty); set => SetValue(CategorySpacingProperty, value); }
        public bool ShowValues { get => (bool)GetValue(ShowValuesProperty); set => SetValue(ShowValuesProperty, value); }
        public string ValueFormat { get => (string)GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
        public double ValueFontSize { get => (double)GetValue(ValueFontSizeProperty); set => SetValue(ValueFontSizeProperty, value); }

        protected override void ComputeDataRange(out double minX, out double maxX, out double minY, out double maxY)
        {
            var series = Series;
            var horizontal = Orientation == Orientation.Horizontal;
            var catAxis = horizontal ? YAxis : XAxis;
            var catCount = 0;
            if (catAxis?.Categories != null) catCount = catAxis.Categories.Count;

            double valueMin = 0, valueMax = 1;
            var foundAny = false;
            if (series != null)
            {
                foreach (var s in series)
                {
                    if (s == null || !s.IsVisible) continue;
                    s.GetArrays(out var _, out var ys);
                    var n = ys?.Length ?? 0;
                    if (catCount == 0) catCount = Math.Max(catCount, n);
                    if (BarMode == BarMode.Stacked)
                    {
                        if (!foundAny) { valueMin = 0; valueMax = 0; }
                        var stack = new double[Math.Max(n, catCount)];
                        for (var i = 0; i < n; i++) stack[i] += ys[i];
                        for (var i = 0; i < stack.Length; i++)
                        {
                            if (stack[i] < valueMin) valueMin = stack[i];
                            if (stack[i] > valueMax) valueMax = stack[i];
                        }
                        foundAny = true;
                        continue;
                    }
                    for (var i = 0; i < n; i++)
                    {
                        var v = ys[i];
                        if (!double.IsFinite(v)) continue;
                        if (!foundAny) { valueMin = Math.Min(0, v); valueMax = Math.Max(0, v); foundAny = true; }
                        else { if (v < valueMin) valueMin = v; if (v > valueMax) valueMax = v; }
                    }
                }
            }
            if (!foundAny) { valueMin = 0; valueMax = 1; }
            if (valueMin == valueMax) valueMax = valueMin + 1;
            if (valueMin > 0) valueMin = 0;

            if (catCount < 1) catCount = 1;

            if (horizontal)
            {
                minX = valueMin; maxX = valueMax;
                minY = 0; maxY = catCount;
            }
            else
            {
                minX = 0; maxX = catCount;
                minY = valueMin; maxY = valueMax;
            }
        }

        protected override void DrawPlot(DrawingContext dc)
        {
            var r = PlotRect;
            if (r.Width <= 0 || r.Height <= 0) return;
            var series = Series;
            if (series == null || series.Count == 0) return;

            var horizontal = Orientation == Orientation.Horizontal;
            var catAxis = horizontal ? YAxis : XAxis;
            var catCount = 0;
            if (catAxis?.Categories != null) catCount = catAxis.Categories.Count;
            else
            {
                foreach (var s in series)
                {
                    if (s == null) continue;
                    s.GetArrays(out var _, out var ys);
                    if (ys != null && ys.Length > catCount) catCount = ys.Length;
                }
            }
            if (catCount <= 0) return;

            var visibleSeries = new List<ChartSeries>();
            foreach (var s in series) if (s != null && s.IsVisible) visibleSeries.Add(s);
            if (visibleSeries.Count == 0) return;

            dc.PushClip(new RectangleGeometry(r));
            try
            {
                var catSpacing = Math.Max(0, Math.Min(0.9, CategorySpacing));
                var barSpacing = Math.Max(0, Math.Min(0.5, BarSpacing));

                if (horizontal)
                {
                    var slot = r.Height / catCount;
                    var slotInner = slot * (1 - catSpacing);
                    var baselineX = DataToViewX(0);
                    var stackPos = new double[catCount];
                    var stackNeg = new double[catCount];

                    for (var sIdx = 0; sIdx < visibleSeries.Count; sIdx++)
                    {
                        var ser = visibleSeries[sIdx];
                        ser.GetArrays(out var _, out var ys);
                        var n = ys?.Length ?? 0;
                        if (n == 0) continue;
                        var fb = GetFrozenBrush(ser.Brush);
                        var groupW = BarMode == BarMode.Grouped ? slotInner / visibleSeries.Count : slotInner;
                        var inner = groupW * (1 - barSpacing);

                        for (var c = 0; c < catCount && c < n; c++)
                        {
                            var v = ys[c];
                            if (!double.IsFinite(v)) continue;
                            var slotCenterY = r.Top + (c + 0.5) * slot;
                            double yTop, yBot;
                            if (BarMode == BarMode.Grouped)
                            {
                                var groupTop = slotCenterY - slotInner / 2 + sIdx * groupW;
                                yTop = groupTop + (groupW - inner) / 2;
                                yBot = yTop + inner;
                            }
                            else
                            {
                                yTop = slotCenterY - inner / 2;
                                yBot = slotCenterY + inner / 2;
                            }
                            double xStart, xEnd;
                            if (BarMode == BarMode.Stacked)
                            {
                                if (v >= 0) { xStart = DataToViewX(stackPos[c]); xEnd = DataToViewX(stackPos[c] + v); stackPos[c] += v; }
                                else { xStart = DataToViewX(stackNeg[c]); xEnd = DataToViewX(stackNeg[c] + v); stackNeg[c] += v; }
                            }
                            else
                            {
                                xStart = baselineX; xEnd = DataToViewX(v);
                            }
                            var rect = new Rect(Math.Min(xStart, xEnd), yTop, Math.Abs(xEnd - xStart), yBot - yTop);
                            if (rect.Width > 0 && rect.Height > 0)
                                dc.DrawRectangle(fb, null, rect);
                            if (ShowValues && rect.Height >= 12)
                            {
                                var label = v.ToString(ValueFormat ?? "0.##");
                                var ft = TextCache.Get(label, ValueFontSize, TextBrush ?? System.Windows.Media.Brushes.Gray);
                                // 가로 막대 → 막대 끝 (오른쪽 또는 왼쪽 음수)
                                var labelX = v >= 0 ? rect.Right + 4 : rect.Left - ft.Width - 4;
                                var labelY = (yTop + yBot) / 2 - ft.Height / 2;
                                if (labelX >= r.Left && labelX + ft.Width <= r.Right)
                                    dc.DrawText(ft, new System.Windows.Point(labelX, labelY));
                            }
                        }
                    }
                }
                else
                {
                    var slot = r.Width / catCount;
                    var slotInner = slot * (1 - catSpacing);
                    var baselineY = DataToViewY(0);
                    var stackPos = new double[catCount];
                    var stackNeg = new double[catCount];

                    for (var sIdx = 0; sIdx < visibleSeries.Count; sIdx++)
                    {
                        var ser = visibleSeries[sIdx];
                        ser.GetArrays(out var _, out var ys);
                        var n = ys?.Length ?? 0;
                        if (n == 0) continue;
                        var fb = GetFrozenBrush(ser.Brush);
                        var groupW = BarMode == BarMode.Grouped ? slotInner / visibleSeries.Count : slotInner;
                        var inner = groupW * (1 - barSpacing);

                        for (var c = 0; c < catCount && c < n; c++)
                        {
                            var v = ys[c];
                            if (!double.IsFinite(v)) continue;
                            var slotCenterX = r.Left + (c + 0.5) * slot;
                            double xLeft, xRight;
                            if (BarMode == BarMode.Grouped)
                            {
                                var groupLeft = slotCenterX - slotInner / 2 + sIdx * groupW;
                                xLeft = groupLeft + (groupW - inner) / 2;
                                xRight = xLeft + inner;
                            }
                            else
                            {
                                xLeft = slotCenterX - inner / 2;
                                xRight = slotCenterX + inner / 2;
                            }
                            double yStart, yEnd;
                            if (BarMode == BarMode.Stacked)
                            {
                                if (v >= 0) { yStart = DataToViewY(stackPos[c]); yEnd = DataToViewY(stackPos[c] + v); stackPos[c] += v; }
                                else { yStart = DataToViewY(stackNeg[c]); yEnd = DataToViewY(stackNeg[c] + v); stackNeg[c] += v; }
                            }
                            else
                            {
                                yStart = baselineY; yEnd = DataToViewY(v);
                            }
                            var rect = new Rect(xLeft, Math.Min(yStart, yEnd), xRight - xLeft, Math.Abs(yEnd - yStart));
                            if (rect.Width > 0 && rect.Height > 0)
                                dc.DrawRectangle(fb, null, rect);
                            if (ShowValues && rect.Width >= 12)
                            {
                                var label = v.ToString(ValueFormat ?? "0.##");
                                var ft = TextCache.Get(label, ValueFontSize, TextBrush ?? System.Windows.Media.Brushes.Gray);
                                // 세로 막대 → 막대 위쪽 (양수) 또는 아래쪽 (음수)
                                var labelX = (xLeft + xRight) / 2 - ft.Width / 2;
                                var labelY = v >= 0 ? rect.Top - ft.Height - 2 : rect.Bottom + 2;
                                if (labelY >= r.Top && labelY + ft.Height <= r.Bottom)
                                    dc.DrawText(ft, new System.Windows.Point(labelX, labelY));
                            }
                        }
                    }
                }
            }
            finally { dc.Pop(); }
        }

        protected override ChartHoverState HitTestHover(System.Windows.Point screenPx)
        {
            var r = PlotRect;
            if (!r.Contains(screenPx)) return ChartHoverState.Empty;
            var horizontal = Orientation == Orientation.Horizontal;
            var catAxis = horizontal ? YAxis : XAxis;
            var series = Series;
            if (series == null) return ChartHoverState.Empty;

            // Visible 시리즈만 모음 + 카테고리 카운트 결정
            var visible = new System.Collections.Generic.List<(int origIdx, ChartSeries ser)>();
            for (var s = 0; s < series.Count; s++)
            {
                var ser = series[s];
                if (ser == null || !ser.IsVisible) continue;
                visible.Add((s, ser));
            }
            if (visible.Count == 0) return ChartHoverState.Empty;

            var catCount = 0;
            if (catAxis?.Categories != null) catCount = catAxis.Categories.Count;
            else
            {
                foreach (var (_, ser) in visible)
                {
                    ser.GetArrays(out var _, out var ys);
                    if (ys != null && ys.Length > catCount) catCount = ys.Length;
                }
            }
            if (catCount <= 0) return ChartHoverState.Empty;

            var catSpacing = Math.Max(0, Math.Min(0.9, CategorySpacing));
            var barSpacing = Math.Max(0, Math.Min(0.5, BarSpacing));

            int catIndex;
            int sIdx;
            ChartSeries pickedSer;
            double pickedVal;

            if (horizontal)
            {
                var slot = r.Height / catCount;
                catIndex = (int)((screenPx.Y - r.Top) / slot);
                if (catIndex < 0 || catIndex >= catCount) return ChartHoverState.Empty;
                var slotInner = slot * (1 - catSpacing);
                var slotCenterY = r.Top + (catIndex + 0.5) * slot;

                if (BarMode == BarMode.Grouped)
                {
                    var groupH = slotInner / visible.Count;
                    var rel = screenPx.Y - (slotCenterY - slotInner / 2);
                    var groupIdx = (int)(rel / groupH);
                    if (groupIdx < 0 || groupIdx >= visible.Count) return ChartHoverState.Empty;
                    sIdx = visible[groupIdx].origIdx;
                    pickedSer = visible[groupIdx].ser;
                }
                else
                {
                    // Stacked / Overlay: 첫 visible 사용 (Stacked는 stack 누적 hit-test가 복잡 → v1 단순화)
                    sIdx = visible[0].origIdx;
                    pickedSer = visible[0].ser;
                }
                pickedSer.GetArrays(out var _, out var ys);
                if (ys == null || catIndex >= ys.Length) return ChartHoverState.Empty;
                pickedVal = ys[catIndex];
                return new ChartHoverState(sIdx, catIndex, pickedVal, catIndex, screenPx, pickedSer.Title);
            }
            else
            {
                var slot = r.Width / catCount;
                catIndex = (int)((screenPx.X - r.Left) / slot);
                if (catIndex < 0 || catIndex >= catCount) return ChartHoverState.Empty;
                var slotInner = slot * (1 - catSpacing);
                var slotCenterX = r.Left + (catIndex + 0.5) * slot;

                if (BarMode == BarMode.Grouped)
                {
                    var groupW = slotInner / visible.Count;
                    var rel = screenPx.X - (slotCenterX - slotInner / 2);
                    var groupIdx = (int)(rel / groupW);
                    if (groupIdx < 0 || groupIdx >= visible.Count) return ChartHoverState.Empty;
                    sIdx = visible[groupIdx].origIdx;
                    pickedSer = visible[groupIdx].ser;
                }
                else
                {
                    sIdx = visible[0].origIdx;
                    pickedSer = visible[0].ser;
                }
                pickedSer.GetArrays(out var _, out var ys);
                if (ys == null || catIndex >= ys.Length) return ChartHoverState.Empty;
                pickedVal = ys[catIndex];
                return new ChartHoverState(sIdx, catIndex, catIndex, pickedVal, screenPx, pickedSer.Title);
            }
        }
    }
}
