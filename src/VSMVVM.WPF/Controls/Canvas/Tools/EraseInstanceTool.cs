using System.Windows;
using System.Windows.Input;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 클릭한 위치의 인스턴스(라벨이 칠해진 영역) 통째로 삭제하는 도구.
    /// 구 DeepInsight 의 EraseAllBrushTool 과 동일한 의미 — Shape 기반이 아니라 픽셀 마스크 기반.
    /// 드래그 시 마우스가 거치는 모든 인스턴스를 차례로 삭제.
    /// </summary>
    public class EraseInstanceTool : CanvasToolBase, IMaskMutatingTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.EraseInstance;
        public override Cursor ToolCursor => Cursors.Cross;

        private bool _isErasing;
        private uint _lastErasedId;

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown) return false;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return false;

            EraseAt(mask, ctx, position);
            _isErasing = true;
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e)
        {
            if (!_isErasing) return;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return;
            EraseAt(mask, ctx, position);
        }

        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            _isErasing = false;
            _lastErasedId = 0;
            ctx.NotifyDrawingCompleted();
        }

        private void EraseAt(VSMVVM.WPF.Controls.MaskLayer mask, CanvasToolContext ctx, Point layeredCanvasPos)
        {
            var p = BrushTool.ToMaskPixel(ctx, layeredCanvasPos);
            int px = (int)p.X;
            int py = (int)p.Y;
            uint id = mask.GetInstanceIdAt(px, py);
            if (id == 0) return;
            if (id == _lastErasedId) return; // 같은 인스턴스 중복 삭제 회피
            _lastErasedId = id;
            mask.DeleteInstance(id);
        }
    }
}