#nullable enable
namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// 파일 열기/저장 다이얼로그를 추상화. ViewModel이 파일 시스템과 독립이 되도록 한다.
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// 파일 열기 다이얼로그를 표시하고 선택된 파일 경로를 반환. 취소 시 null.
        /// </summary>
        /// <param name="filter">WPF OpenFileDialog Filter 형식 (예: "Image|*.png;*.jpg").</param>
        string? OpenFile(string filter);

        /// <summary>
        /// 파일 저장 다이얼로그를 표시하고 지정된 경로를 반환. 취소 시 null.
        /// </summary>
        string? SaveFile(string filter, string? suggestedName = null);
    }
}
