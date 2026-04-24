using System.Windows;
using System.Windows.Input;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 이미지 이동(pan) 전용 도구. 모든 드래그를 LayeredCanvas 가 Pan 으로 처리하도록
    /// false 를 반환. Mode = Arrow 를 LayeredCanvas 가 감지해 무조건 StartPan.
    /// </summary>
    public sealed class ArrowTool : CanvasToolBase
    {
        public override CanvasToolMode Mode => CanvasToolMode.Arrow;
        public override Cursor ToolCursor => Cursors.Hand;

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) => false;
        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e) { }
        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) { }
    }
}
