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
            _points.Add(BrushTool.ToMaskPixel(ctx, position));
            mask.BeginStroke(ResolveLabelIndex(ctx));
            _active = true;
            RaisePreviewChanged();
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e)
        {
            if (!_active) return;
            _points.Add(BrushTool.ToMaskPixel(ctx, position));
            RaisePreviewChanged();
        }

        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (!_active) return;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) { _active = false; return; }

            _points.Add(BrushTool.ToMaskPixel(ctx, position));
            int label = ResolveLabelIndex(ctx);
            if (_points.Count >= 3)
            {
                // Vertex 편집용 점은 freehand 원본이 너무 많으므로 최소 간격으로 간소화.
                var savedPoints = Simplify(_points, minSpacing: 6.0);
                mask.PaintPolygon(_points, label);
                mask.EndStroke(label);
                var inst = mask.Instances.GetById(mask.LastCreatedInstanceId);
                if (inst != null) inst.PolygonPoints = savedPoints;
            }
            else
            {
                mask.CancelStroke();
            }
            _points.Clear();
            _active = false;
            RaisePreviewChanged();
            ctx.NotifyDrawingCompleted();
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
