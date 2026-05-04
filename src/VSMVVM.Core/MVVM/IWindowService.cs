namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 윈도우 서비스 인터페이스. View가 직접 <see cref="System.Windows.Window"/>를 상속한 경우
    /// 별도의 호스트 래핑 없이 그대로 띄웁니다. 자체 헤더/버튼을 가진 풍부한 UI에 적합합니다.
    /// IDialogService와 달리 OK/Cancel 버튼을 자동으로 추가하지 않습니다.
    /// </summary>
    public interface IWindowService
    {
        /// <summary>
        /// 모달 윈도우를 표시합니다.
        /// </summary>
        DialogResult<TResult> ShowWindow<TResult>(string windowName, double width, double height);

        /// <summary>
        /// 파라미터와 함께 모달 윈도우를 표시합니다.
        /// ViewModel의 <c>DialogParameter</c> 프로퍼티(있으면)에 자동 주입됩니다.
        /// 닫힘 시 ViewModel의 <c>DialogResultData</c> 프로퍼티(있으면)에서 결과를 회수합니다.
        /// </summary>
        DialogResult<TResult> ShowWindow<TResult, TParam>(string windowName, double width, double height, TParam param);

        /// <summary>
        /// 모덜리스(비차단) 윈도우를 표시합니다.
        /// </summary>
        void Show(string windowName, double width, double height);
    }
}
