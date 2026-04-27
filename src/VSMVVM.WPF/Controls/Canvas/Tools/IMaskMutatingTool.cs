#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 마스크 픽셀 데이터를 변경하는 도구의 마커 인터페이스.
    /// MaskBehavior 가 stroke 전 before 스냅샷을 뜨고 완료 시 Undo 에 push 할 대상 판별에 사용한다.
    /// </summary>
    public interface IMaskMutatingTool
    {
    }
}
