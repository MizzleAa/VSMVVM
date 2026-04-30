using System;
using System.Collections.Generic;

namespace VSMVVM.WPF.Design.Components.Charts.Downsamplers
{
    public static class PixelBucketDownsampler
    {
        public static void Downsample(
            double[] xs, double[] ys, int start, int end,
            Func<double, double> dataToViewX, Func<double, double> dataToViewY,
            double viewLeft, double viewTop, double viewWidth, double viewHeight,
            out double[] outXs, out double[] outYs)
        {
            var n = end - start;
            if (n <= 0 || viewWidth <= 0 || viewHeight <= 0)
            {
                outXs = Array.Empty<double>(); outYs = Array.Empty<double>(); return;
            }

            var seen = new HashSet<long>(Math.Min(n, 65536));
            var ox = new List<double>(Math.Min(n, 65536));
            var oy = new List<double>(Math.Min(n, 65536));

            var maxPx = (int)Math.Ceiling(viewWidth);
            var maxPy = (int)Math.Ceiling(viewHeight);

            for (var i = start; i < end; i++)
            {
                var x = xs[i]; var y = ys[i];
                if (!double.IsFinite(x) || !double.IsFinite(y)) continue;
                var px = (int)(dataToViewX(x) - viewLeft);
                var py = (int)(dataToViewY(y) - viewTop);
                if (px < 0 || py < 0 || px >= maxPx || py >= maxPy) continue;
                long key = ((long)px << 20) | (uint)py;
                if (seen.Add(key)) { ox.Add(x); oy.Add(y); }
            }

            outXs = ox.ToArray();
            outYs = oy.ToArray();
        }
    }
}
