using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 자유곡선 freehand 폴리곤: 좌클릭 후 드래그하며 점 누적, MouseUp 에서 완성.
    /// Escape 로 취소. 3점 미만이면 자동 취소.
    /// </summary>
    public class PolygonMaskTool : CanvasToolBase, IInteractiveCanvasTool, IMaskMutatingTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.PolygonMask;
        public override Cursor ToolCursor => Cursors.Pen;

        private readonly List<Point> _points = new();
        private bool _active;
        // BeginStroke 가 발번한 tentative 인스턴스 ID. EndStroke 후 LastCreatedInstanceId 와 비교해
        // 새 인스턴스(=일치) vs 기존 인스턴스에 합쳐짐(=불일치) 을 판정한다.
        private uint _tentativeId;

        public bool IsInputSessionActive => _active;

        /// <summary>진행 중 점들(마스크 픽셀 좌표). ShapePreviewLayer 가 렌더에 사용.</summary>
        public IReadOnlyList<Point> CurrentPoints => _points;

        public event EventHandler? PreviewChanged;
        private void RaisePreviewChanged() => PreviewChanged?.Invoke(this, EventArgs.Empty);

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown) return false;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return false;

            _points.Clear();
            _points.Add(ClampToMask(BrushTool.ToMaskPixel(ctx, position), mask));
            _tentativeId = mask.BeginStroke(ResolveLabelIndex(ctx));
            _active = true;
            RaisePreviewChanged();
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e)
        {
            if (!_active) return;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return;
            _points.Add(ClampToMask(BrushTool.ToMaskPixel(ctx, position), mask));
            RaisePreviewChanged();
        }

        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (!_active) return;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) { _active = false; return; }

            _points.Add(ClampToMask(BrushTool.ToMaskPixel(ctx, position), mask));
            int label = ResolveLabelIndex(ctx);
            if (_points.Count >= 3)
            {
                // Vertex 편집용 점은 freehand 원본이 너무 많으므로 최소 간격으로 간소화.
                var savedPoints = Simplify(_points, minSpacing: 6.0);
                mask.PaintPolygon(_points, label);
                mask.EndStroke(label);
                // 같은 라벨 위에 stroke 가 겹치면 EndStroke 가 기존 인스턴스로 합치고 PolygonContours=null 로 무효화.
                // 이때 새 polygon 외곽만 PolygonPoints 에 박으면 합쳐진 모양 vertex 편집이 깨진다.
                // 새 인스턴스(tentativeId 가 그대로 LastCreatedInstanceId) 일 때만 freehand 외곽 보존.
                if (mask.LastCreatedInstanceId == _tentativeId)
                {
                    var inst = mask.Instances.GetById(mask.LastCreatedInstanceId);
                    if (inst != null) inst.PolygonPoints = savedPoints;
                }
                // else: PolygonContours=null 유지 → 다음 더블클릭 시 EnsurePolygonPoints 가 합쳐진 마스크에서 새로 추출.
            }
            else
            {
                mask.CancelStroke();
            }
            _points.Clear();
            _active = false;
            _tentativeId = 0;
            RaisePreviewChanged();
            ctx.NotifyDrawingCompleted();
        }

        // 마우스가 마스크 영역 밖으로 나가도 polygon 점이 마스크 범위로 clamp 되도록.
        // PaintPolygon 이 음수/초과 좌표를 받으면 fill scan 이 가장자리에 잘못된 띠를 칠하는 버그를 차단.
        private static Point ClampToMask(Point p, VSMVVM.WPF.Controls.MaskLayer mask)
        {
            double maxX = Math.Max(0, mask.MaskWidth - 1);
            double maxY = Math.Max(0, mask.MaskHeight - 1);
            return new Point(
                Math.Max(0, Math.Min(maxX, p.X)),
                Math.Max(0, Math.Min(maxY, p.Y)));
        }

        // Enter / DblClick 제거 — MouseUp 만으로 완성.
        public bool OnEnterPressed(CanvasToolContext ctx) => false;
        public bool OnDoubleClick(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) => false;

        public bool OnEscapePressed(CanvasToolContext ctx)
        {
            if (!_active) return false;
            ctx.TargetMaskLayer?.CancelStroke();
            _points.Clear();
            _active = false;
            _tentativeId = 0;
            RaisePreviewChanged();
            return true;
        }

        protected virtual int ResolveLabelIndex(CanvasToolContext ctx)
            => ctx.TargetMaskLayer?.CurrentLabelIndex ?? 1;

        /// <summary>연속 점들 중 이전 채택점과 minSpacing 미만 거리면 스킵. 마지막 점은 항상 포함.</summary>
        internal static List<Point> Simplify(IReadOnlyList<Point> src, double minSpacing)
        {
            var res = new List<Point>();
            if (src.Count == 0) return res;
            res.Add(src[0]);
            double min2 = minSpacing * minSpacing;
            for (int i = 1; i < src.Count - 1; i++)
            {
                var prev = res[^1];
                double dx = src[i].X - prev.X, dy = src[i].Y - prev.Y;
                if (dx * dx + dy * dy >= min2) res.Add(src[i]);
            }
            if (src.Count > 1 && !SamePoint(res[^1], src[^1])) res.Add(src[^1]);
            return res;
        }

        private static bool SamePoint(Point a, Point b) => a.X == b.X && a.Y == b.Y;
    }
}
