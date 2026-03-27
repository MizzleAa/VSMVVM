namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 스플래시 화면 서비스 인터페이스. 앱 시작 시 로딩 프로그레스와 상태 메시지를 표시합니다.
    /// </summary>
    public interface ISplashService
    {
        /// <summary>
        /// 프로그레스와 상태 메시지를 보고합니다.
        /// </summary>
        /// <param name="message">표시할 메시지.</param>
        /// <param name="progress">진행률 (0.0 ~ 1.0).</param>
        void Report(string message, double progress);

        /// <summary>
        /// 메시지만 보고합니다 (무한 프로그레스).
        /// </summary>
        void Report(string message);

        /// <summary>
        /// 스플래시를 수동으로 닫습니다.
        /// </summary>
        void Close();
    }
}
