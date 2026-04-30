using System;

namespace VSMVVM.WPF.Design.Components.Charts.Downsamplers
{
    public sealed class LttbDownsampler : ILineDownsampler
    {
        public void Downsample(double[] xs, double[] ys, int start, int end, int targetBuckets,
                               out double[] outXs, out double[] outYs)
        {
            var n = end - start;
            if (n <= 0) { outXs = Array.Empty<double>(); outYs = Array.Empty<double>(); return; }
            if (targetBuckets >= n || targetBuckets < 3)
            {
                outXs = new double[n]; outYs = new double[n];
                Array.Copy(xs, start, outXs, 0, n);
                Array.Copy(ys, start, outYs, 0, n);
                return;
            }

            outXs = new double[targetBuckets];
            outYs = new double[targetBuckets];

            outXs[0] = xs[start]; outYs[0] = ys[start];
            outXs[targetBuckets - 1] = xs[end - 1]; outYs[targetBuckets - 1] = ys[end - 1];

            var bucketSize = (double)(n - 2) / (targetBuckets - 2);
            var a = start;

            for (var i = 0; i < targetBuckets - 2; i++)
            {
                var rangeStart = (int)Math.Floor((i + 1) * bucketSize) + start + 1;
                var rangeEnd = (int)Math.Floor((i + 2) * bucketSize) + start + 1;
                if (rangeEnd > end) rangeEnd = end;
                if (rangeStart >= rangeEnd) rangeStart = rangeEnd - 1;
                if (rangeStart < start + 1) rangeStart = start + 1;

                double avgX = 0, avgY = 0; var avgCnt = 0;
                var avgRangeStart = rangeEnd;
                var avgRangeEnd = (int)Math.Floor((i + 3) * bucketSize) + start + 1;
                if (avgRangeEnd > end) avgRangeEnd = end;
                for (var j = avgRangeStart; j < avgRangeEnd; j++)
                {
                    avgX += xs[j]; avgY += ys[j]; avgCnt++;
                }
                if (avgCnt > 0) { avgX /= avgCnt; avgY /= avgCnt; }
                else { avgX = xs[end - 1]; avgY = ys[end - 1]; }

                var pointAx = xs[a];
                var pointAy = ys[a];
                var maxArea = -1.0;
                var maxIdx = rangeStart;
                for (var j = rangeStart; j < rangeEnd; j++)
                {
                    var area = Math.Abs((pointAx - avgX) * (ys[j] - pointAy)
                                       - (pointAx - xs[j]) * (avgY - pointAy)) * 0.5;
                    if (area > maxArea) { maxArea = area; maxIdx = j; }
                }
                outXs[i + 1] = xs[maxIdx];
                outYs[i + 1] = ys[maxIdx];
                a = maxIdx;
            }
        }
    }
}
