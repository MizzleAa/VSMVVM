using System;
using System.Collections.Generic;
using System.Windows;

#nullable enable
namespace VSMVVM.WPF.Imaging
{
    /// <summary>
    /// Magnetic Lasso 용 Live-wire 알고리즘. Sobel gradient 를 비용으로 Dijkstra 최단경로.
    /// 엣지가 강할수록 비용 낮음 → 경로가 엣지를 따라감.
    /// </summary>
    public static class LiveWire
    {
        private const double COST_MAX = 1.0;
        private const int UNREACHABLE = -1;

        /// <summary>
        /// 지정 window 범위 내에서 source 로부터 Dijkstra 최단 경로 prev 배열 계산.
        /// prev[idx] = 이전 픽셀의 global index. unreachable 은 <see cref="UNREACHABLE"/>.
        /// window 밖 픽셀도 prev 배열에 포함되지만 값은 UNREACHABLE.
        /// </summary>
        /// <param name="gradient">전체 이미지 gradient magnitude (width*height).</param>
        /// <param name="width">이미지 너비.</param>
        /// <param name="height">이미지 높이.</param>
        /// <param name="srcX">source 픽셀 x.</param>
        /// <param name="srcY">source 픽셀 y.</param>
        /// <param name="windowRect">Dijkstra 탐색 창 (픽셀 단위). null 이면 전체.</param>
        /// <param name="edgeContrast">0~1. gMax 대비 이 비율 미만의 gradient 는 엣지로 취급하지 않아 고비용(1.0) 처리. 0 이면 비활성.</param>
        /// <returns>prev[y*width+x] 배열.</returns>
        public static int[] RunDijkstra(double[] gradient, int width, int height,
            int srcX, int srcY, Rect? windowRect = null, double edgeContrast = 0.0)
        {
            int total = width * height;
            var prev = new int[total];
            var dist = new double[total];
            for (int i = 0; i < total; i++) { prev[i] = UNREACHABLE; dist[i] = double.PositiveInfinity; }

            int wx0 = 0, wy0 = 0, wx1 = width - 1, wy1 = height - 1;
            if (windowRect is Rect rw)
            {
                wx0 = Math.Max(0, (int)rw.X);
                wy0 = Math.Max(0, (int)rw.Y);
                wx1 = Math.Min(width - 1, (int)(rw.X + rw.Width));
                wy1 = Math.Min(height - 1, (int)(rw.Y + rw.Height));
            }

            if ((uint)srcX >= (uint)width || (uint)srcY >= (uint)height) return prev;

            int srcIdx = srcY * width + srcX;
            dist[srcIdx] = 0;
            var pq = new PriorityQueue<int, double>();
            pq.Enqueue(srcIdx, 0);

            // gradient 최대값(정규화용).
            double gMax = 0;
            for (int i = 0; i < gradient.Length; i++) if (gradient[i] > gMax) gMax = gradient[i];
            if (gMax < 1.0) gMax = 1.0;

            int[] dxArr = { 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] dyArr = { 0, 0, 1, -1, 1, -1, 1, -1 };
            double[] moveCost = { 1, 1, 1, 1, 1.4142, 1.4142, 1.4142, 1.4142 };

            while (pq.TryDequeue(out int idx, out double d))
            {
                if (d > dist[idx]) continue;
                int x = idx % width;
                int y = idx / width;
                for (int k = 0; k < 8; k++)
                {
                    int nx = x + dxArr[k];
                    int ny = y + dyArr[k];
                    if (nx < wx0 || nx > wx1 || ny < wy0 || ny > wy1) continue;
                    int nIdx = ny * width + nx;
                    // 다음 픽셀 엣지 비용: 엣지 강할수록 낮음. 0~1.
                    double normalized = gradient[nIdx] / gMax;
                    // edgeContrast cutoff: 약한 엣지(< threshold)는 엣지 아님으로 간주 → 고비용.
                    double effective = normalized < edgeContrast ? 0.0 : normalized;
                    double edgeCost = COST_MAX - effective;
                    // 너무 균일한 영역은 고비용(1), 엣지는 저비용(≈0).
                    double newDist = d + edgeCost * moveCost[k];
                    if (newDist < dist[nIdx])
                    {
                        dist[nIdx] = newDist;
                        prev[nIdx] = idx;
                        pq.Enqueue(nIdx, newDist);
                    }
                }
            }
            return prev;
        }

        /// <summary>Dijkstra 결과 prev 로 dst → src 역추적하여 Point 경로(dst → src 순서 역전 없이 dst가 끝).</summary>
        public static IReadOnlyList<Point> Backtrack(int[] prev, int width, int dstX, int dstY)
        {
            var path = new List<Point>();
            int idx = dstY * width + dstX;
            int safety = width * 4; // 무한 루프 방어.
            while (idx != UNREACHABLE && safety-- > 0)
            {
                int x = idx % width;
                int y = idx / width;
                path.Add(new Point(x, y));
                int p = prev[idx];
                if (p == idx) break;
                idx = p;
            }
            path.Reverse();
            return path;
        }
    }
}
