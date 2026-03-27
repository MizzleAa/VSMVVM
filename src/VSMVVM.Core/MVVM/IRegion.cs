namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// Region 콘텐츠 인터페이스. WPFRegion 등에서 구현합니다.
    /// </summary>
    public interface IRegion
    {
        /// <summary>
        /// 현재 Region에 표시 중인 콘텐츠.
        /// </summary>
        object Content { get; set; }
    }
}
