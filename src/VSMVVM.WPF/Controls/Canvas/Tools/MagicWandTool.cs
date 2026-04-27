using System.Windows;
using System.Windows.Input;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 클릭한 픽셀의 RGB 와 Tolerance 이내인 4-연결 contiguous 영역을 현재 라벨의 마스크 인스턴스로.
    /// 한 번 클릭으로 완결.
    /// </summary>
    public class MagicWandTool : CanvasToolBase, IMaskMutatingTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.MagicWand;
        public override Cursor ToolCursor => Cursors.Cross;

        private int _tolerance = 32;
        /// <summary>0-255 범위 RGB 허용 오차. 기본 32.</summary>
        public int Tolerance
        {
            get => _tolerance;
            set { _tolerance = System.Math.Max(0, System.Math.Min(255, value)); OnPropertyChanged(); }
        }

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown) return false;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return false;
            var p = BrushTool.ToMaskPixel(ctx, position);
            int label = mask.CurrentLabelIndex;
            if (label == 0) return false;
            mask.BeginStroke(label);
            mask.PaintRgbFloodFill((int)p.X, (int)p.Y, label, _tolerance);
            mask.EndStroke(label);
            ctx.NotifyDrawingCompleted();
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e) { }
        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) { }
    }
}
