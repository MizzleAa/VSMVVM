using System.Windows;
using System.Windows.Input;

namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 선택/이동 도구. LayeredCanvas의 기존 로직을 사용합니다.
    /// </summary>
    public class SelectTool : ICanvasTool
    {
        public CanvasToolMode Mode => CanvasToolMode.Select;
        public Cursor ToolCursor => Cursors.Arrow;

        // Select 모드는 LayeredCanvas의 기존 마우스 로직에 위임.
        // OnMouseDown에서 false를 반환하여 LayeredCanvas가 자체 처리.
        public bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) => false;
        public void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e) { }
        public void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) { }
    }
}
