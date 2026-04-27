using System.Windows;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>드래그로 타원 영역을 그려 마스크 인스턴스로 만든다. Shift=정원.</summary>
    public class EllipseMaskTool : RectangleMaskTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.EllipseMask;

        protected override void RasterizeFinal(MaskLayer mask, Rect rect, int label)
            => mask.PaintEllipse(rect, label);
    }
}
