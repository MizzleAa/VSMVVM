using System;
using System.Windows;
using System.Windows.Input;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 드래그로 사각형 영역을 그려 마스크 인스턴스로 만든다.
    /// OnMouseDown=시작점 + BeginStroke, OnMouseMove=실시간 preview 갱신, OnMouseUp=PaintRectangle + EndStroke.
    /// Shift=정사각형 제약. 이미지 경계 밖 영역은 자동 clamp.
    /// </summary>
    public class RectangleMaskTool : CanvasToolBase, IMaskMutatingTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.RectangleMask;
        public override Cursor ToolCursor => Cursors.Cross;

        private Point? _start;

        /// <summary>드래그 중 preview 용 현재 Rect(마스크 픽셀 좌표). null = preview 없음.</summary>
        public Rect? CurrentRect { get; private set; }

        /// <summary>CurrentRect 변경 알림 — ShapePreviewLayer 가 구독.</summary>
        public event EventHandler? PreviewChanged;
        private void RaisePreviewChanged() => PreviewChanged?.Invoke(this, EventArgs.Empty);

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown) return false;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return false;
            _start = BrushTool.ToMaskPixel(ctx, position);
            CurrentRect = new Rect(_start.Value, _start.Value);
            RaisePreviewChanged();
            mask.BeginStroke(ResolveLabelIndex(ctx));
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e)
        {
            if (_start == null) return;
            var mask = ctx.TargetMaskLayer;
            var cur = BrushTool.ToMaskPixel(ctx, position);
            var rect = CalcBounds(_start.Value, cur, ctx.IsShiftDown);
            if (mask != null) rect = ClampToImage(rect, mask);
            CurrentRect = rect;
            RaisePreviewChanged();
        }

        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            var mask = ctx.TargetMaskLayer;
            if (mask == null || _start == null) return;
            var cur = BrushTool.ToMaskPixel(ctx, position);
            var rect = CalcBounds(_start.Value, cur, ctx.IsShiftDown);
            rect = ClampToImage(rect, mask);
            if (rect.Width >= 1 && rect.Height >= 1)
                RasterizeFinal(mask, rect, ResolveLabelIndex(ctx));
            mask.EndStroke(ResolveLabelIndex(ctx));
            _start = null;
            CurrentRect = null;
            RaisePreviewChanged();
            ctx.NotifyDrawingCompleted();
        }

        protected virtual void RasterizeFinal(MaskLayer mask, Rect rect, int label)
            => mask.PaintRectangle(rect, label);

        protected virtual int ResolveLabelIndex(CanvasToolContext ctx)
            => ctx.TargetMaskLayer?.CurrentLabelIndex ?? 1;

        protected static Rect CalcBounds(Point a, Point b, bool square)
        {
            double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
            double w = Math.Abs(b.X - a.X), h = Math.Abs(b.Y - a.Y);
            if (square)
            {
                double s = Math.Max(w, h);
                x = b.X < a.X ? a.X - s : a.X;
                y = b.Y < a.Y ? a.Y - s : a.Y;
                w = s; h = s;
            }
            return new Rect(x, y, w, h);
        }

        private static Rect ClampToImage(Rect r, MaskLayer mask)
        {
            var bounds = new Rect(0, 0, mask.MaskWidth, mask.MaskHeight);
            r.Intersect(bounds);
            return r.IsEmpty ? new Rect(0, 0, 0, 0) : r;
        }
    }
}
