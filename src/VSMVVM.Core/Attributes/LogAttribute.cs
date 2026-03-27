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
    /// 메서드에 적용하면 Source Generator가 AOP 스타일 로깅 래퍼를 생성합니다.
    /// 진입/퇴출/실행시간/예외를 자동 기록합니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class LogAttribute : Attribute
    {
        /// <summary>
        /// 로그 레벨. 기본값은 Info.
        /// </summary>
        public LogLevel Level { get; set; } = LogLevel.Info;
    }
}
