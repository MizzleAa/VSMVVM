using System.Windows.Input;
using VSMVVM.WPF.Imaging;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 브러시 패턴과 동일하되 라벨 인덱스를 0(배경)으로 강제하는 지우개.
    /// </summary>
    public class EraserTool : BrushTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.Eraser;
        public override Cursor ToolCursor => Cursors.Cross;

        protected override int ResolveLabelIndex(CanvasToolContext ctx)
            => LabelClassCollection.BackgroundIndex;

        protected override void BeginLifecycle(VSMVVM.WPF.Controls.MaskLayer mask, int labelIndex)
            => mask.BeginErase();

        protected override void EndLifecycle(VSMVVM.WPF.Controls.MaskLayer mask, int labelIndex)
            => mask.EndErase();
    }
}
