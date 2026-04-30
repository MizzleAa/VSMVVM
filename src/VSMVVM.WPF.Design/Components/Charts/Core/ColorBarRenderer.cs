using System;
using System.Windows;
using System.Windows.Media;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    /// <summary>
    /// 차트 오른쪽에 colormap 컬러바와 눈금 라벨을 그린다.
    /// </summary>
    public static class ColorBarRenderer
    {
        public const double DefaultBarWidth = 12;
        public const double DefaultLabelGap = 4;
        public const double DefaultLeftMargin = 8;
        public const double DefaultLabelWidth = 36;

        public static double TotalWidth => DefaultBarWidth + DefaultLeftMargin + DefaultLabelGap + DefaultLabelWidth;

        public static void Draw(DrawingContext dc, Rect plotRect, double totalActualWidth,
                                LinearGradientBrush gradient,
                                double minValue, double maxValue,
                                Brush textBrush, FormattedTextCache textCache,
                                int tickCount = 5)
        {
            if (gradient == null || textBrush == null || textCache == null) return;
            var barLeft = plotRect.Right + DefaultLeftMargin;
            var barTop = plotRect.Top;
            var barH = plotRect.Height;
            if (barLeft + DefaultBarWidth + DefaultLabelGap + 4 > totalActualWidth) return;

            dc.DrawRectangle(gradient, null, new Rect(barLeft, barTop, DefaultBarWidth, barH));

            if (!double.IsFinite(minValue) || !double.IsFinite(maxValue) || maxValue <= minValue) return;

            var ticks = ChartTickGenerator.Generate(minValue, maxValue, tickCount);
            foreach (var t in ticks)
            {
                var rel = (t - minValue) / (maxValue - minValue);
                if (rel < 0 || rel > 1) continue;
                var y = barTop + (1 - rel) * barH;
                var ft = textCache.Get(FormatValue(t), 10, textBrush);
                dc.DrawText(ft, new Point(barLeft + DefaultBarWidth + DefaultLabelGap, y - ft.Height / 2));
            }
        }

        private static string FormatValue(double v)
        {
            var abs = Math.Abs(v);
            if (abs == 0) return "0";
            if (abs >= 1e6 || abs < 1e-3) return v.ToString("0.##E+0");
            if (abs >= 100) return v.ToString("0");
            if (abs >= 1) return v.ToString("0.##");
            return v.ToString("0.###");
        }
    }
}
