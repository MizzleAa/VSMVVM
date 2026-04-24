#nullable enable
namespace VSMVVM.WPF.Controls.Behaviors
{
    /// <summary>
    /// VM → Behavior 방향의 1회성 I/O 요청 토큰. VM이 새 인스턴스를 바인딩 프로퍼티에 set 하면
    /// <see cref="MaskBehavior"/>가 이를 감지해 파일 경로 기반으로 Export/Import를 수행한다.
    /// </summary>
    /// <remarks>
    /// 동일 경로를 재요청하려면 반드시 새 인스턴스를 할당해야 한다(DP 변경 감지를 위해).
    /// </remarks>
    public sealed class MaskIoRequest
    {
        public MaskIoRequest(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}
