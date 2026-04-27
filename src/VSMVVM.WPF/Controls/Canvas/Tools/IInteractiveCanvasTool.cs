using System.Windows;
using System.Windows.Input;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 연속 클릭 + Enter / Escape / 더블클릭으로 완료되는 도구. Polygon / Angle 측정 등.
    /// LayeredCanvas 가 CurrentTool 이 이 인터페이스를 구현한 경우 키/더블클릭 이벤트를 전달한다.
    /// </summary>
    public interface IInteractiveCanvasTool : ICanvasTool
    {
        /// <summary>Enter 키 — 완료.</summary>
        bool OnEnterPressed(CanvasToolContext ctx);

        /// <summary>Escape 키 — 취소.</summary>
        bool OnEscapePressed(CanvasToolContext ctx);

        /// <summary>더블클릭 — 완료.</summary>
        bool OnDoubleClick(CanvasToolContext ctx, Point position, MouseButtonEventArgs e);

        /// <summary>
        /// true 이면 LayeredCanvas 가 MouseUp 후에도 CanvasToolContext 를 유지 —
        /// 연속 클릭 중 stroke 상태 보존용.
        /// </summary>
        bool IsInputSessionActive { get; }
    }
}
