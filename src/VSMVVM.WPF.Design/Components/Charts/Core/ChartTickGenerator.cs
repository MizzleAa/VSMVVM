using System;
using System.Collections.Generic;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public static class ChartTickGenerator
    {
        public static IList<double> Generate(double min, double max, int targetCount)
        {
            var result = new List<double>();
            if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min || targetCount < 2)
            {
                if (double.IsFinite(min) && double.IsFinite(max) && max > min)
                {
                    result.Add(min);
                    result.Add(max);
                }
                return result;
            }

            var range = NiceNum(max - min, false);
            var step = NiceNum(range / (targetCount - 1), true);
            var graphMin = Math.Floor(min / step) * step;
            var graphMax = Math.Ceiling(max / step) * step;

            for (var v = graphMin; v <= graphMax + step * 0.5; v += step)
            {
                if (v >= min - step * 1e-6 && v <= max + step * 1e-6)
                {
                    result.Add(Math.Round(v / step) * step);
                }
            }
            return result;
        }

        private static double NiceNum(double x, bool round)
        {
            if (x <= 0) return 1;
            var exp = Math.Floor(Math.Log10(x));
            var f = x / Math.Pow(10, exp);
            double nf;
            if (round)
            {
                if (f < 1.5) nf = 1;
                else if (f < 3) nf = 2;
                else if (f < 7) nf = 5;
                else nf = 10;
            }
            else
            {
                if (f <= 1) nf = 1;
                else if (f <= 2) nf = 2;
                else if (f <= 5) nf = 5;
                else nf = 10;
            }
            return nf * Math.Pow(10, exp);
        }
    }
}
