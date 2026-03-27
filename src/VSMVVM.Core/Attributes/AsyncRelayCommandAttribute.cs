using System;

namespace VSMVVM.Core.Attributes
{
    /// <summary>
    /// 비동기 메서드에 적용하면 Source Generator가 AsyncRelayCommand 프로퍼티를 생성합니다.
    /// IsRunning 프로퍼티가 자동으로 포함됩니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class AsyncRelayCommandAttribute : Attribute
    {
        /// <summary>
        /// CanExecute 판정 메서드 이름. null이면 항상 실행 가능 (IsRunning 제외).
        /// </summary>
        public string CanExecute { get; set; }
    }
}
