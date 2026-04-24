using System.Collections.Generic;

#nullable enable
namespace VSMVVM.WPF.Imaging.Coco
{
    /// <summary>
    /// 2D 마스크에서 외곽 contour 를 추출하는 Moore-neighbor tracing.
    /// 구멍은 무시(외곽만). COCO polygon 생성용.
    /// </summary>
    internal static class ContourTracing
    {
        /// <summary>
        /// instanceMask 에서 target == targetId 픽셀로 구성된 영역들의 외곽 contour 목록을 반환.
        /// 각 contour 는 [x0,y0, x1,y1, ...] 형식의 평면 좌표 배열.
        /// 컴포넌트가 여럿이면 여러 contour. 각 픽셀의 중심을 point 로 사용(+0.5 보정).
        /// </summary>
        public static List<List<double>> TraceInstanceOutlines(
            uint[] instanceMask, int width, int height, uint targetId)
        {
            var contours = new List<List<double>>();
            var visited = new bool[instanceMask.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
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

            int maxSteps = w * h * 8;
            for (int step = 0; step < maxSteps; step++)
            {
                bool found = false;
                // 이전 방향에서 시계 +1 에 해당하는 방향부터 검사. (반시계는 -1)
                // 실제로는 Moore 스펙: 시작 방향은 이전 진입 방향의 "오른쪽" = prevDir - 2 (mod 8).
                int startDir = (prevDir + 6) % 8; // 이전 방향의 90° CCW (오른쪽 이웃부터 시작)
                for (int k = 0; k < 8; k++)
                {
                    int dir = (startDir + k) % 8;
                    int nx = cx + dx[dir];
                    int ny = cy + dy[dir];
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
                    if (mask[ny * w + nx] != id) continue;

                    cx = nx; cy = ny;
                    prevDir = dir;
                    pts.Add(cx + 0.5); pts.Add(cy + 0.5);
                    found = true;
                    break;
                }

                if (!found) break; // 고립 픽셀
                if (cx == sx && cy == sy && pts.Count > 4) break; // 루프 닫힘
            }

            return pts;
        }
    }
}
