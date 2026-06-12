using System;

namespace VSMVVM.Core.Attributes
{
    /// <summary>
    /// 로그 레벨 열거형 (NLog LogLevel과 매핑).
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Trace.
        /// </summary>
        Trace,

        /// <summary>
        /// Debug.
        /// </summary>
        Debug,

        /// <summary>
        /// Info.
        /// </summary>
        Info,

        /// <summary>
        /// Warn.
        /// </summary>
        Warn,

        /// <summary>
        /// Error.
        /// </summary>
        Error
    }

    /// <summary>
    /// 메서드 또는 필드에 적용하면 Source Generator가 자동 로깅 코드를 삽입합니다.
    /// - [RelayCommand]/[AsyncRelayCommand] 메서드: Command 람다 안에 ILoggerService 진입 로그.
    /// - [Property] 필드: 생성된 setter 안 값 변경 시 ILoggerService 로 새 값 기록.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class LogAttribute : Attribute
    {
        /// <summary>
        /// 로그 레벨. 기본값은 Info.
        /// </summary>
        public LogLevel Level { get; set; } = LogLevel.Info;
    }
}
