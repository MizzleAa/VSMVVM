using System.Windows;
using System.Windows.Input;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 4연결 flood fill 버킷 도구. 클릭 지점의 라벨 연결 영역을 현재 라벨로 덮어쓴다.
    /// </summary>
    public class FillTool : CanvasToolBase, IMaskMutatingTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.Fill;
        public override Cursor ToolCursor => Cursors.Cross;

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown) return false;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return false;

            var label = mask.CurrentLabelIndex;
            mask.BeginStroke(label);
            var pixel = BrushTool.ToMaskPixel(ctx, position);
            mask.FloodFill(pixel, label);
            mask.EndStroke(label);
            ctx.NotifyDrawingCompleted();
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e) { }
        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) { }
    }
}
