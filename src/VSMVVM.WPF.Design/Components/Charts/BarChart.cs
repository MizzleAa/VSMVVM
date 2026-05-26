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

        /// <summary>
        /// <see cref="ShowValues"/> 가 true 면 시리즈 최댓값을 <see cref="ValueFormat"/> 으로 포맷한 문자열의
        /// 폭/높이를 측정해서 plot 영역 우측(가로) 또는 상단(세로) 여백을 확보한다.
        /// 가로 막대 음수 값이 있는 경우는 좌측은 categorical 라벨 영역이 이미 있어 별도 처리 불필요.
        /// </summary>
        protected override (double extraRight, double extraTop) MeasureValueLabelOverhead()
        {
            if (!ShowValues) return (0, 0);
            var series = Series;
            if (series == null || series.Count == 0) return (0, 0);

            double maxAbs = 0;
            foreach (var s in series)
            {
                if (s == null || !s.IsVisible) continue;
                s.GetArrays(out var _, out var ys);
                if (ys == null) continue;
                foreach (var y in ys)
                {
                    if (!double.IsFinite(y)) continue;
                    var a = Math.Abs(y);
                    if (a > maxAbs) maxAbs = a;
                }
            }
            if (maxAbs <= 0) return (0, 0);

            var label = maxAbs.ToString(ValueFormat ?? "0.##");
            var ft = TextCache.Get(label, ValueFontSize, TextBrush ?? Brushes.Gray);

            if (Orientation == Orientation.Horizontal)
            {
                // BarChart.DrawPlot 가 라벨을 막대 끝 + 4px 위치에 그리므로 동일 패딩 + 약간 여유.
                return (ft.Width + 8, 0);
            }
            // 세로: 막대 위 + 2px 위치에 그림.
            return (0, ft.Height + 4);
        }

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

            // ShowValues 라벨이 plot 영역 우측(가로) / 상단(세로) 밖의 여백에 그려질 수 있으므로,
            // PushClip 을 PlotRect 가 아니라 컨트롤 전체로 둔다. AutoFitAxisLabels 가 여백을 이미 확보했으므로 안전.
            // 막대/선 자체 좌표는 DataToView* 변환이 PlotRect 기준이므로 plot 영역 밖으로 새지 않음.
            var clipRect = ShowValues
                ? new Rect(0, 0, ActualWidth, ActualHeight)
                : r;
            dc.PushClip(new RectangleGeometry(clipRect));
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
                        // 시리즈 단위 fallback 색. 카테고리별 색이 지정된 경우 c 루프 안에서 덮어씀.
                        var seriesBrush = GetFrozenBrush(ser.Brush);
                        var perCat = ser.BrushPerCategory;
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
                            var fullRect = new Rect(Math.Min(xStart, xEnd), yTop, Math.Abs(xEnd - xStart), yBot - yTop);
                            // 가로 막대: width 만 progress 비례 — baseline(x=0) 에서부터 자라남.
                            var animW = fullRect.Width * AnimationProgress;
                            var rect = v >= 0
                                ? new Rect(fullRect.X, fullRect.Y, animW, fullRect.Height)
                                : new Rect(fullRect.Right - animW, fullRect.Y, animW, fullRect.Height);
                            var fb = (perCat != null && c < perCat.Count && perCat[c] != null)
                                ? GetFrozenBrush(perCat[c])
                                : seriesBrush;
                            if (rect.Width > 0 && rect.Height > 0)
                                dc.DrawRectangle(fb, null, rect);
                            // 값 라벨은 거의 완료(99%) 시점에 그려 깜빡임/이동감 방지.
                            // 라벨을 막대 안쪽 시작점(baseline 쪽 끝) 에 그려 plot 끝에 닿는 가장 큰 막대도 잘리지 않도록.
                            if (ShowValues && rect.Height >= 12 && AnimationProgress >= 0.99)
                            {
                                var label = v.ToString(ValueFormat ?? "0.##");
                                var ft = TextCache.Get(label, ValueFontSize, TextBrush ?? System.Windows.Media.Brushes.Gray);
                                var labelX = v >= 0 ? fullRect.Left + 4 : fullRect.Right - ft.Width - 4;
                                var labelY = (yTop + yBot) / 2 - ft.Height / 2;
                                // 막대 너비보다 라벨이 길면 그리지 않음 (막대 밖으로 새는 것 방지).
                                if (ft.Width + 8 <= fullRect.Width)
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
                        var seriesBrush = GetFrozenBrush(ser.Brush);
                        var perCat = ser.BrushPerCategory;
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
                            var fullRect = new Rect(xLeft, Math.Min(yStart, yEnd), xRight - xLeft, Math.Abs(yEnd - yStart));
                            // 세로 막대: height 만 progress 비례 — baseline(y=0) 에서부터 위/아래로 자라남.
                            var animH = fullRect.Height * AnimationProgress;
                            var rect = v >= 0
                                ? new Rect(fullRect.X, fullRect.Bottom - animH, fullRect.Width, animH)
                                : new Rect(fullRect.X, fullRect.Y, fullRect.Width, animH);
                            var fb = (perCat != null && c < perCat.Count && perCat[c] != null)
                                ? GetFrozenBrush(perCat[c])
                                : seriesBrush;
                            if (rect.Width > 0 && rect.Height > 0)
                                dc.DrawRectangle(fb, null, rect);
                            if (ShowValues && rect.Width >= 12 && AnimationProgress >= 0.99)
                            {
                                var label = v.ToString(ValueFormat ?? "0.##");
                                var ft = TextCache.Get(label, ValueFontSize, TextBrush ?? System.Windows.Media.Brushes.Gray);
                                var labelX = (xLeft + xRight) / 2 - ft.Width / 2;
                                // 막대 안쪽 상단(양수) / 하단(음수) — 가장 큰 막대도 plot 안에 그려져 잘리지 않음.
                                var labelY = v >= 0 ? fullRect.Top + 2 : fullRect.Bottom - ft.Height - 2;
                                // 막대 높이보다 라벨이 크면 그리지 않음.
                                if (ft.Height + 4 <= fullRect.Height)
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
