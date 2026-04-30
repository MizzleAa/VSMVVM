using System;
using System.Windows;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public static class ChartHitTester
    {
        public static ChartHoverState NearestPoint(ChartBase chart, Point screenPx)
        {
            var series = chart.Series;
            if (series == null) return ChartHoverState.Empty;

            var bestSeries = -1;
            var bestPoint = -1;
            var bestDist = double.MaxValue;
            double bestDataX = 0, bestDataY = 0;
            string bestTitle = null;
            const double pickRadiusPx = 16.0;

            for (var s = 0; s < series.Count; s++)
            {
                var ser = series[s];
                if (ser == null || !ser.IsVisible) continue;
                ser.GetArrays(out var xs, out var ys, out var n);
                if (n == 0) continue;

                var idx = NearestIndexBySortedX(xs, n, chart.ViewToDataX(screenPx.X));
                var lo = Math.Max(0, idx - 2);
                var hi = Math.Min(n - 1, idx + 2);
                for (var i = lo; i <= hi; i++)
                {
                    var px = chart.DataToViewX(xs[i]);
                    var py = chart.DataToViewY(ys[i]);
                    var dx = px - screenPx.X;
                    var dy = py - screenPx.Y;
                    var d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestSeries = s;
                        bestPoint = i;
                        bestDataX = xs[i];
                        bestDataY = ys[i];
                        bestTitle = ser.Title;
                    }
                }
            }

            if (bestSeries < 0 || bestDist > pickRadiusPx) return ChartHoverState.Empty;
            var sp = new Point(chart.DataToViewX(bestDataX), chart.DataToViewY(bestDataY));
            return new ChartHoverState(bestSeries, bestPoint, bestDataX, bestDataY, sp, bestTitle);
        }

        public static int NearestIndexBySortedX(double[] xs, int count, double targetX)
        {
            if (count <= 0) return 0;
            int lo = 0, hi = count - 1;
            if (targetX <= xs[0]) return 0;
            if (targetX >= xs[count - 1]) return count - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) >> 1;
                if (xs[mid] < targetX) lo = mid + 1;
                else hi = mid - 1;
            }
            if (lo >= count) lo = count - 1;
            if (lo > 0 && Math.Abs(xs[lo - 1] - targetX) < Math.Abs(xs[lo] - targetX)) lo -= 1;
            return lo;
        }
    }
}
