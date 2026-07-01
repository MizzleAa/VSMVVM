using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>
    /// I.2b — ExecutionRun.Records 를 가로 막대(간트 차트)로 렌더.
    /// 각 막대 = 한 노드의 한 번 실행. X = 시작 시각(상대), W = Elapsed.
    /// 색상은 디자인 토큰 (성공=AccentSecondary, 실패=Error).
    /// </summary>
    public sealed class GanttChartView : FrameworkElement
    {
        public static readonly DependencyProperty RunProperty = DependencyProperty.Register(
            nameof(Run), typeof(ExecutionRun), typeof(GanttChartView),
            new FrameworkPropertyMetadata(default(ExecutionRun),
                // Records 개수에 따라 DesiredHeight 가 달라지므로 AffectsMeasure 필수.
                // 빠지면 ScrollViewer 가 첫 측정값(빈 Run = 40px)을 그대로 캐시하여 스크롤바가 표시되지 않음.
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsRender));

        public ExecutionRun Run
        {
            get => (ExecutionRun)GetValue(RunProperty);
            set => SetValue(RunProperty, value);
        }

        /// <summary>브레이크포인트가 걸린 노드 Id 집합 — 해당 행은 막대/레이블 모두 Error 색으로 강조.</summary>
        public static readonly DependencyProperty BreakpointsProperty = DependencyProperty.Register(
            nameof(Breakpoints), typeof(IEnumerable<Guid>), typeof(GanttChartView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public IEnumerable<Guid> Breakpoints
        {
            get => (IEnumerable<Guid>)GetValue(BreakpointsProperty);
            set => SetValue(BreakpointsProperty, value);
        }

        private const double RowHeight = 18;
        private const double RowSpacing = 2;
        // 레이블/막대 영역의 고정 비율 — 레이블 폭과 무관하게 막대 시작점을 항상 동일하게 정렬.
        private const double LeftLabelWidth = 220; // 레이블 클리핑 폭 (긴 typeId 도 같은 X 에서 끊김)
        private const double LabelGutter = 8;      // 레이블과 막대 사이 간격
        private const double DurationLabelGutter = 6; // 막대와 ms 텍스트 사이 간격
        private const double DurationLabelWidth = 70;  // ms 텍스트 영역 (오른쪽 예약)
        private const double FontSize = 11;

        protected override Size MeasureOverride(Size availableSize)
        {
            int n = Run?.Records?.Count ?? 0;
            double h = Math.Max(40, n * (RowHeight + RowSpacing) + 12);
            double w = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
            return new Size(w, h);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var bg = TryBrush("BgPrimary", Brushes.Black);
            dc.DrawRectangle(bg, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (Run == null || Run.Records.Count == 0)
            {
                var text = FormatText("(no records)", TryBrush("TextMuted", Brushes.Gray));
                dc.DrawText(text, new Point(8, 8));
                return;
            }

            // 가장 빠른 시작 시각 기준 상대 좌표.
            DateTimeOffset t0 = Run.Records[0].StartedAt;
            double totalMs = (Run.Elapsed > TimeSpan.Zero ? Run.Elapsed.TotalMilliseconds : 1);
            if (totalMs <= 0) totalMs = 1;

            // 막대 영역 — 항상 LeftLabelWidth + Gutter 에서 시작 (레이블 길이와 무관).
            double plotLeft = LeftLabelWidth + LabelGutter;
            double plotRight = Math.Max(plotLeft + 50, ActualWidth - DurationLabelGutter - DurationLabelWidth - 8);
            double plotWidth = plotRight - plotLeft;

            var okBrush = TryBrush("AccentSecondary", Brushes.SteelBlue);
            var errBrush = TryBrush("Error", Brushes.Crimson);
            var labelBrush = TryBrush("TextSecondary", Brushes.LightGray);
            var durationBrush = TryBrush("TextMuted", Brushes.LightGray);

            // 브레이크포인트 집합 (HashSet 으로 O(1) 조회).
            HashSet<Guid> bpSet = null;
            if (Breakpoints != null)
            {
                bpSet = new HashSet<Guid>(Breakpoints);
            }

            var bpStrokePen = new Pen(errBrush, 1.5);
            bpStrokePen.Freeze();

            for (int i = 0; i < Run.Records.Count; i++)
            {
                var r = Run.Records[i];
                double y = 6 + i * (RowHeight + RowSpacing);
                bool isBreakpoint = bpSet != null && bpSet.Contains(r.NodeId);

                // 레이블 (TypeId) — LeftLabelWidth 영역 안으로 클리핑. 브레이크포인트 행은 Error 색.
                var label = FormatText(r.TypeId, isBreakpoint ? errBrush : labelBrush);
                label.MaxTextWidth = LeftLabelWidth;
                label.MaxTextHeight = RowHeight;
                label.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(label, new Point(4, y + 1));

                // 막대 — 모든 막대가 plotLeft 에서 정렬되어 시작 (시간 축 기준 위치 표시).
                double startMs = (r.StartedAt - t0).TotalMilliseconds;
                double x = plotLeft + (startMs / totalMs) * plotWidth;
                double w = Math.Max(2, (r.Elapsed.TotalMilliseconds / totalMs) * plotWidth);
                // 채움 색: 브레이크포인트 우선 → 실패(Error) → 성공(AccentSecondary)
                var fill = isBreakpoint ? errBrush : (r.Success ? okBrush : errBrush);
                if (isBreakpoint)
                {
                    // 브레이크포인트 행은 보더도 Error 로 두껍게 둘러서 한층 강조.
                    dc.DrawRectangle(fill, bpStrokePen, new Rect(x, y, w, RowHeight));
                }
                else
                {
                    dc.DrawRectangle(fill, null, new Rect(x, y, w, RowHeight));
                }

                // 막대 우측 — Elapsed 를 ms 단위로 출력. 브레이크포인트 행은 강조.
                var ms = r.Elapsed.TotalMilliseconds;
                string duration = ms >= 10 ? $"{ms:F0} ms" : $"{ms:F2} ms";
                var durText = FormatText(duration, isBreakpoint ? errBrush : durationBrush);
                dc.DrawText(durText, new Point(plotRight + DurationLabelGutter, y + 1));
            }
        }

        private static FormattedText FormatText(string s, Brush brush)
        {
            return new FormattedText(s,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono"),
                FontSize, brush, 1.0);
        }

        private static Brush TryBrush(string key, Brush fallback)
        {
            var res = Application.Current?.TryFindResource(key);
            return res as Brush ?? fallback;
        }
    }
}
