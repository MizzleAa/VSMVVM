using System;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// NLog 통합 로거 서비스 인터페이스.
    /// </summary>
    public interface ILoggerService
    {
        /// <summary>
        /// NLog 설정 파일을 로드합니다.
        /// </summary>
        void Configure(string configFilePath);

        /// <summary>
        /// Trace 레벨 로그.
        /// </summary>
        void Trace(string message);

        /// <summary>
        /// Debug 레벨 로그.
        /// </summary>
        void Debug(string message);

        /// <summary>
        /// Info 레벨 로그.
        /// </summary>
        void Info(string message);

        /// <summary>
        /// Warn 레벨 로그.
        /// </summary>
        void Warn(string message);

        /// <summary>
        /// Error 레벨 로그.
        /// </summary>
        void Error(string message);

        /// <summary>
        /// Error 레벨 로그 (예외 포함).
        /// </summary>
        void Error(string message, Exception exception);

        /// <summary>
        /// Fatal 레벨 로그.
        /// </summary>
        void Fatal(string message);

        /// <summary>
        /// Fatal 레벨 로그 (예외 포함).
        /// </summary>
        void Fatal(string message, Exception exception);
    }
}
