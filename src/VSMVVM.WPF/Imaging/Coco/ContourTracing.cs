using System.Collections.Generic;
using System.Windows;

#nullable enable
namespace VSMVVM.WPF.Imaging.Coco
{
    /// <summary>
    /// 2D 마스크에서 외곽 contour 를 추출하는 Moore-neighbor tracing.
    /// 구멍은 무시(외곽만). COCO polygon 생성용.
    /// </summary>
    public static class ContourTracing
    {
        /// <summary>
        /// instanceMask 에서 target == targetId 픽셀로 구성된 영역들의 외곽 contour 목록을 반환.
        /// 각 contour 는 [x0,y0, x1,y1, ...] 형식의 평면 좌표 배열.
        /// 컴포넌트가 여럿이면 여러 contour. 각 픽셀의 중심을 point 로 사용(+0.5 보정).
        /// </summary>
        public static List<List<double>> TraceInstanceOutlines(
            uint[] instanceMask, int width, int height, uint targetId)
        {
            return TraceInstanceOutlinesScoped(instanceMask, width, height, targetId, 0, 0, width, height);
        }

        /// <summary>
        /// <see cref="TraceInstanceOutlines"/> 의 BBox 스코프 버전. 외곽 raster scan 을 [bx,bx+bw) × [by,by+bh) 만 돌아
        /// 8K 이미지에서 인스턴스 BBox 가 작을 때 visited 배열 전체 스캔을 회피한다.
        /// MooreTrace / MarkComponentVisited 는 mask 전체 범위 인덱싱을 그대로 사용 (boundary 가 BBox 를 약간 넘어가도 안전).
        /// </summary>
        public static List<List<double>> TraceInstanceOutlinesScoped(
            uint[] instanceMask, int width, int height, uint targetId,
            int bx, int by, int bw, int bh)
        {
            var contours = new List<List<double>>();
            var visited = new bool[instanceMask.Length];

            int y0 = System.Math.Max(0, by);
            int x0 = System.Math.Max(0, bx);
            int y1 = System.Math.Min(height, by + bh);
            int x1 = System.Math.Min(width, bx + bw);

            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    int idx = y * width + x;
                    if (visited[idx] || instanceMask[idx] != targetId) continue;

                    // 새 컴포넌트 시작점(raster scan 최상단-좌측). Moore trace 한 번.
                    var contour = MooreTrace(instanceMask, width, height, targetId, x, y);
                    if (contour.Count >= 6) // 최소 3점(x,y 쌍)
                        contours.Add(contour);

                    // 이 컴포넌트의 모든 픽셀을 flood-fill 로 visited 표시 (추출된 contour 뿐 아니라 내부까지).
                    MarkComponentVisited(instanceMask, width, height, targetId, x, y, visited);
                }
            }

            return contours;
        }

        private static void MarkComponentVisited(uint[] mask, int w, int h, uint id, int sx, int sy, bool[] visited)
        {
            var stack = new Stack<(int x, int y)>();
            stack.Push((sx, sy));
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                if ((uint)x >= (uint)w || (uint)y >= (uint)h) continue;
                int idx = y * w + x;
                if (visited[idx]) continue;
                if (mask[idx] != id) continue;
                visited[idx] = true;
                stack.Push((x + 1, y));
                stack.Push((x - 1, y));
                stack.Push((x, y + 1));
                stack.Push((x, y - 1));
            }
        }

        /// <summary>
        /// Moore-neighbor boundary tracing. 시작점 (sx,sy) 는 raster scan 순서상 최상단-좌측 target 픽셀.
        /// 8-방향 시계 순회로 경계를 따라 움직이며 좌표 기록. 시작점으로 돌아오면 종료.
        /// </summary>
        private static List<double> MooreTrace(uint[] mask, int w, int h, uint id, int sx, int sy)
        {
            var pts = new List<double>();
            // 8방향: 0=E, 1=SE, 2=S, 3=SW, 4=W, 5=NW, 6=N, 7=NE
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };

            int cx = sx, cy = sy;
            pts.Add(cx + 0.5); pts.Add(cy + 0.5);

            // 시작 방향은 "바로 왼쪽(West)" → Moore 는 이전 방향에서 시계방향 +1 부터 탐색.
            int prevDir = 4; // W
            int firstExitDir = -1; // Jacob's stopping criterion: 시작점에서 첫 픽셀로 떠나는 출발 방향.

            int maxSteps = w * h * 8;
            bool done = false;
            for (int step = 0; step < maxSteps && !done; step++)
            {
                // 시작점 재도달 후 다음 step: 첫 매치 방향이 firstExitDir 와 같으면 한 바퀴 완료.
                bool atStart = (cx == sx && cy == sy && step > 0);

                bool found = false;
                // 이전 방향의 90° CCW 부터 시계 방향으로 8 이웃 탐색.
                int startDir = (prevDir + 6) % 8;
                for (int k = 0; k < 8; k++)
                {
                    int dir = (startDir + k) % 8;
                    int nx = cx + dx[dir];
                    int ny = cy + dy[dir];
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
                    if (mask[ny * w + nx] != id) continue;

                    // 시작점에서 처음과 같은 방향으로 다시 출발하려 하면 외곽 한 바퀴 완성 — 이동하지 말고 종료.
                    if (atStart && dir == firstExitDir) { done = true; break; }

                    cx = nx; cy = ny;
                    prevDir = dir;
                    if (firstExitDir < 0) firstExitDir = dir;
                    pts.Add(cx + 0.5); pts.Add(cy + 0.5);
                    found = true;
                    break;
                }

                if (done) break;
                if (!found) break; // 고립 픽셀.
            }

            // 종료 시 마지막 점이 시작점과 같으면 중복 제거 (rasterize / adorner 모두 implicit close 가정).
            if (pts.Count >= 4)
            {
                int last = pts.Count - 2;
                if (System.Math.Abs(pts[last] - pts[0]) < 1e-9 && System.Math.Abs(pts[last + 1] - pts[1]) < 1e-9)
                {
                    pts.RemoveAt(pts.Count - 1);
                    pts.RemoveAt(pts.Count - 1);
                }
            }
            return pts;
        }

        /// <summary>
        /// Marching Squares 로 마스크의 외곽 contour 를 추출. 다중 폐곡선이면 가장 긴 것만 반환.
        /// 자세한 설명은 <see cref="ExtractContoursAll"/> 참조.
        /// </summary>
        public static List<Point> ExtractContour(
            uint[] mask, int width, int height, uint targetId,
            int bx, int by, int bw, int bh)
        {
            var all = ExtractContoursAll(mask, width, height, targetId, bx, by, bw, bh);
            if (all.Count == 0) return new List<Point>();
            // 가장 긴 폐곡선 선택.
            var best = all[0];
            for (int i = 1; i < all.Count; i++)
                if (all[i].Count > best.Count) best = all[i];
            return best;
        }

        /// <summary>
        /// Marching Squares 로 마스크의 모든 폐곡선(외곽 + hole 들) 추출. BBox 를 +1 padding 한 셀 grid 를 순회하며
        /// 각 셀의 4 corner (TL, TR, BR, BL) 매치 여부 4-bit 코드로 16 패턴 lookup → 0~2 개 선분 출력.
        /// 모든 선분 endpoint 를 dictionary 로 체이닝해 폐곡선 구성. 좌표는 픽셀 corner 의 0.5 offset (정수 + 0.5).
        /// 외곽/hole 분류는 호출 측 책임 (가장 긴 것 = 외곽 + point-in-polygon 등).
        /// </summary>
        public static List<List<Point>> ExtractContoursAll(
            uint[] mask, int width, int height, uint targetId,
            int bx, int by, int bw, int bh)
        {
            // 셀 grid 순회 범위 — BBox 외곽을 +1 padding 한 cell 좌표.
            int cxMin = System.Math.Max(0, bx);
            int cyMin = System.Math.Max(0, by);
            int cxMax = System.Math.Min(width, bx + bw + 1);
            int cyMax = System.Math.Min(height, by + bh + 1);

            var next = new Dictionary<long, Point>();

            for (int cy = cyMin; cy <= cyMax; cy++)
            {
                for (int cx = cxMin; cx <= cxMax; cx++)
                {
                    int tl = SampleCorner(mask, width, height, cx - 1, cy - 1, targetId);
                    int tr = SampleCorner(mask, width, height, cx,     cy - 1, targetId);
                    int br = SampleCorner(mask, width, height, cx,     cy,     targetId);
                    int bl = SampleCorner(mask, width, height, cx - 1, cy,     targetId);
                    int code = (tl << 3) | (tr << 2) | (br << 1) | bl;
                    if (code == 0 || code == 15) continue;

                    var T = new Point(cx - 0.5, cy - 1.0);
                    var R = new Point(cx,        cy - 0.5);
                    var B = new Point(cx - 0.5, cy);
                    var L = new Point(cx - 1.0, cy - 0.5);

                    // 시계방향 외곽 순회 = 마스크 안쪽이 항상 진행 방향의 우측. 위키피디아 marching squares 표준.
                    switch (code)
                    {
                        case 1:  AddSeg(next, B, L); break;
                        case 2:  AddSeg(next, R, B); break;
                        case 3:  AddSeg(next, R, L); break;
                        case 4:  AddSeg(next, T, R); break;
                        case 5:  AddSeg(next, B, L); AddSeg(next, T, R); break; // saddle (BL+TR)
                        case 6:  AddSeg(next, T, B); break;
                        case 7:  AddSeg(next, T, L); break;
                        case 8:  AddSeg(next, L, T); break;
                        case 9:  AddSeg(next, B, T); break;
                        case 10: AddSeg(next, L, T); AddSeg(next, R, B); break; // saddle (TL+BR)
                        case 11: AddSeg(next, R, T); break;
                        case 12: AddSeg(next, L, R); break;
                        case 13: AddSeg(next, B, R); break;
                        case 14: AddSeg(next, L, B); break;
                    }
                }
            }

            var result = new List<List<Point>>();
            if (next.Count == 0) return result;

            // 체이닝: 모든 폐곡선 수집.
            var visitedKeys = new HashSet<long>();
            foreach (var kv in next)
            {
                if (visitedKeys.Contains(kv.Key)) continue;
                var loop = new List<Point>();
                long startKey = kv.Key;
                Point cur = KeyToPoint(startKey);
                loop.Add(cur);
                visitedKeys.Add(startKey);
                int safety = next.Count + 8;
                while (safety-- > 0)
                {
                    long curKey = PointToKey(cur);
                    if (!next.TryGetValue(curKey, out var nxt)) break;
                    long nxtKey = PointToKey(nxt);
                    if (nxtKey == startKey) break; // 폐곡선 닫힘.
                    if (visitedKeys.Contains(nxtKey)) break;
                    visitedKeys.Add(nxtKey);
                    loop.Add(nxt);
                    cur = nxt;
                }
                if (loop.Count >= 3) result.Add(loop);
            }
            return result;
        }

        private static int SampleCorner(uint[] mask, int w, int h, int x, int y, uint id)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) return 0;
            return mask[y * w + x] == id ? 1 : 0;
        }

        private static int SampleCorner(SparseTileLayer mask, int w, int h, int x, int y, uint id)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) return 0;
            return mask.Get(x, y) == id ? 1 : 0;
        }

        /// <summary>SparseTileLayer 오버로드 — Marching Squares 가 SampleCorner 만 호출하므로 분기 한 곳만.</summary>
        public static List<Point> ExtractContour(
            SparseTileLayer mask, int width, int height, uint targetId,
            int bx, int by, int bw, int bh)
        {
            var all = ExtractContoursAll(mask, width, height, targetId, bx, by, bw, bh);
            if (all.Count == 0) return new List<Point>();
            var best = all[0];
            for (int i = 1; i < all.Count; i++)
                if (all[i].Count > best.Count) best = all[i];
            return best;
        }

        /// <summary>SparseTileLayer 오버로드 — 다중 폐곡선 (외곽 + hole) 추출.</summary>
        public static List<List<Point>> ExtractContoursAll(
            SparseTileLayer mask, int width, int height, uint targetId,
            int bx, int by, int bw, int bh)
        {
            int cxMin = System.Math.Max(0, bx);
            int cyMin = System.Math.Max(0, by);
            int cxMax = System.Math.Min(width, bx + bw + 1);
            int cyMax = System.Math.Min(height, by + bh + 1);

            var next = new Dictionary<long, Point>();

            for (int cy = cyMin; cy <= cyMax; cy++)
            {
                for (int cx = cxMin; cx <= cxMax; cx++)
                {
                    int tl = SampleCorner(mask, width, height, cx - 1, cy - 1, targetId);
                    int tr = SampleCorner(mask, width, height, cx,     cy - 1, targetId);
                    int br = SampleCorner(mask, width, height, cx,     cy,     targetId);
                    int bl = SampleCorner(mask, width, height, cx - 1, cy,     targetId);
                    int code = (tl << 3) | (tr << 2) | (br << 1) | bl;
                    if (code == 0 || code == 15) continue;

                    var T = new Point(cx - 0.5, cy - 1.0);
                    var R = new Point(cx,        cy - 0.5);
                    var B = new Point(cx - 0.5, cy);
                    var L = new Point(cx - 1.0, cy - 0.5);

                    switch (code)
                    {
                        case 1:  AddSeg(next, B, L); break;
                        case 2:  AddSeg(next, R, B); break;
                        case 3:  AddSeg(next, R, L); break;
                        case 4:  AddSeg(next, T, R); break;
                        case 5:  AddSeg(next, B, L); AddSeg(next, T, R); break;
                        case 6:  AddSeg(next, T, B); break;
                        case 7:  AddSeg(next, T, L); break;
                        case 8:  AddSeg(next, L, T); break;
                        case 9:  AddSeg(next, B, T); break;
                        case 10: AddSeg(next, L, T); AddSeg(next, R, B); break;
                        case 11: AddSeg(next, R, T); break;
                        case 12: AddSeg(next, L, R); break;
                        case 13: AddSeg(next, B, R); break;
                        case 14: AddSeg(next, L, B); break;
                    }
                }
            }

            var result = new List<List<Point>>();
            if (next.Count == 0) return result;

            var visitedKeys = new HashSet<long>();
            foreach (var kv in next)
            {
                if (visitedKeys.Contains(kv.Key)) continue;
                var loop = new List<Point>();
                long startKey = kv.Key;
                Point cur = KeyToPoint(startKey);
                loop.Add(cur);
                visitedKeys.Add(startKey);
                int safety = next.Count + 8;
                while (safety-- > 0)
                {
                    long curKey = PointToKey(cur);
                    if (!next.TryGetValue(curKey, out var nxt)) break;
                    long nxtKey = PointToKey(nxt);
                    if (nxtKey == startKey) break;
                    if (visitedKeys.Contains(nxtKey)) break;
                    visitedKeys.Add(nxtKey);
                    loop.Add(nxt);
                    cur = nxt;
                }
                if (loop.Count >= 3) result.Add(loop);
            }
            return result;
        }

        private static long PointToKey(Point p)
        {
            // 좌표는 정수 또는 정수+0.5. *2 후 정수화로 안전한 키. 8K 이미지에서도 *2 = 16384 < 2^31.
            long xk = (long)System.Math.Round(p.X * 2.0);
            long yk = (long)System.Math.Round(p.Y * 2.0);
            return (xk << 32) | (yk & 0xffffffffL);
        }

        private static Point KeyToPoint(long key)
        {
            long ykMasked = key & 0xffffffffL;
            // 32비트 부호 확장은 필요 없음 (좌표는 항상 0 이상).
            long xk = key >> 32;
            return new Point(xk * 0.5, ykMasked * 0.5);
        }

        private static void AddSeg(Dictionary<long, Point> next, Point a, Point b)
        {
            // a → b 방향 선분. dictionary [a] = b. 이미 있으면 덮어쓰지 않음 (saddle 도 두 선분이 endpoint 가 다름).
            long ka = PointToKey(a);
            if (!next.ContainsKey(ka)) next[ka] = b;
        }
    }
}
