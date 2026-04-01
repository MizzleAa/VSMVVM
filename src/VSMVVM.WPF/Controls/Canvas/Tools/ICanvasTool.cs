using System.Windows;
using System.Windows.Input;

namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 캔버스 그리기 도구의 추상 인터페이스.
    /// LayeredCanvas가 마우스 이벤트를 현재 Tool에 위임합니다.
    /// </summary>
    public interface ICanvasTool
    {
        /// <summary>도구 모드 식별자.</summary>
        CanvasToolMode Mode { get; }

        /// <summary>도구 활성 시 표시할 커서.</summary>
        Cursor ToolCursor { get; }

        /// <summary>
        /// 마우스 다운. true 반환 시 e.Handled=true (Pan 차단).
        /// </summary>
        bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e);

        /// <summary>마우스 이동.</summary>
        void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e);

        /// <summary>마우스 업.</summary>
        void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e);
    }
}
