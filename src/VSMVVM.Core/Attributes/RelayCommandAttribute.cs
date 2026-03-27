using System;

namespace VSMVVM.Core.Attributes
{
    /// <summary>
    /// 메서드에 적용하면 Source Generator가 자동으로 RelayCommand 프로퍼티를 생성합니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class RelayCommandAttribute : Attribute
    {
        /// <summary>
        /// CanExecute 판정 메서드 이름. null이면 항상 실행 가능.
        /// </summary>
        public string CanExecute { get; set; }
    }
}
