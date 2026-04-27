using System.Collections.Generic;
using System.Windows;

#nullable enable
namespace VSMVVM.WPF.Imaging
{
    /// <summary>
    /// Douglas-Peucker (RDP) polyline 단순화. 직선 구간은 점 2 개로 줄이고 곡률 큰 구간은 점 keep.
    /// 모서리·꺾임 자동 보존.
    /// </summary>
    internal static class PolygonSimplifier
    {
        /// <summary>
        /// 폐곡선(closed polyline) 단순화. 시작점이 임의이므로 첫 점에서 가장 먼 점을 둘째 분할점으로 잡아
        /// 두 호에 각각 DP 적용 → 시작점 위치에 영향받지 않는 결정론적 결과.
        /// </summary>
        public static List<Point> SimplifyClosed(IReadOnlyList<Point> points, double epsilon)
        {
            int n = points.Count;
            if (n <= 3) return new List<Point>(points);

            // 점 0 에서 가장 먼 점 = 분할 anchor.
            int anchor = 0;
            double maxD2 = -1;
            var p0 = points[0];
            for (int i = 1; i < n; i++)
            {
                double dx = points[i].X - p0.X, dy = points[i].Y - p0.Y;
                double d2 = dx * dx + dy * dy;
                if (d2 > maxD2) { maxD2 = d2; anchor = i; }
            }

            var keep = new bool[n];
            keep[0] = true;
            keep[anchor] = true;

            // 두 호에 각각 DP 재귀.
            DPRecurse(points, 0, anchor, epsilon, keep);
            DPRecurse(points, anchor, n - 1, epsilon, keep);
            // [n-1] → [0] 폐쇄 segment 도 포함되도록 마지막 호도 처리.
            // 단, 위 두 재귀가 [0..anchor], [anchor..n-1] 을 모두 커버하고, 폐쇄는 [n-1]→[0] 한 변으로 자동 닫힘.
            // 이 변의 중간에 있는 점은 없으므로 별도 재귀 불필요.

            var result = new List<Point>();
            for (int i = 0; i < n; i++)
                if (keep[i]) result.Add(points[i]);
            return result;
        }

        /// <summary>
        /// [start, end] segment 에서 가장 먼 점 P 의 수직 거리가 epsilon 이상이면 keep 표시 후 양쪽 재귀.
        /// epsilon 미만이면 중간 점 모두 버림 (start, end 만 유지).
        /// </summary>
        private static void DPRecurse(IReadOnlyList<Point> points, int start, int end, double epsilon, bool[] keep)
        {
            if (end <= start + 1) return;

            var a = points[start];
            var b = points[end];
            double maxD = -1;
            int maxI = -1;
            for (int i = start + 1; i < end; i++)
            {
                double d = PerpendicularDistance(points[i], a, b);
                if (d > maxD) { maxD = d; maxI = i; }
            }

            if (maxD < epsilon || maxI < 0) return;

            keep[maxI] = true;
            DPRecurse(points, start, maxI, epsilon, keep);
            DPRecurse(points, maxI, end, epsilon, keep);
        }

        /// <summary>점 p 에서 선분 a-b 까지의 수직 거리. a == b 이면 a 로의 직선 거리.</summary>
        private static double PerpendicularDistance(Point p, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-12)
            {
                double ex = p.X - a.X, ey = p.Y - a.Y;
                return System.Math.Sqrt(ex * ex + ey * ey);
            }
            // |(p-a) × (b-a)| / |b-a|
            double cross = (p.X - a.X) * dy - (p.Y - a.Y) * dx;
            return System.Math.Abs(cross) / System.Math.Sqrt(lenSq);
        }
    }
}
