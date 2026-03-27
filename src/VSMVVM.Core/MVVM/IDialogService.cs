namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 다이얼로그 서비스 인터페이스. View/ViewModel 재사용, 버튼 프리셋, 파일 다이얼로그를 지원합니다.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// 모달 다이얼로그를 표시합니다.
        /// </summary>
        DialogResult<TResult> ShowDialog<TResult>(string viewName, double width, double height, DialogButtons buttons = DialogButtons.OKCancel);

        /// <summary>
        /// 파라미터와 함께 모달 다이얼로그를 표시합니다.
        /// </summary>
        DialogResult<TResult> ShowDialog<TResult, TParam>(string viewName, double width, double height, TParam param, DialogButtons buttons = DialogButtons.OKCancel);

        /// <summary>
        /// 모덜리스(비차단) 팝업을 표시합니다.
        /// </summary>
        void Show(string viewName, double width, double height);

        /// <summary>
        /// 파일 열기 다이얼로그.
        /// </summary>
        string[] OpenFileDialog(string initialDirectory, string title, string filter, bool multiselect = false);

        /// <summary>
        /// 파일 저장 다이얼로그.
        /// </summary>
        string SaveFileDialog(string initialDirectory, string title, string filter);

        /// <summary>
        /// 폴더 선택 다이얼로그.
        /// </summary>
        string[] OpenFolderDialog(string initialDirectory, string title);
    }
}
