using System;

namespace VSMVVM.Core.Attributes
{
    /// <summary>
    /// [Property]와 함께 사용하여 프로퍼티 변경 시 추가 프로퍼티의 PropertyChanged도 발생시킵니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public sealed class PropertyChangedForAttribute : Attribute
    {
        /// <summary>
        /// 연쇄 갱신할 프로퍼티 이름.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// 연쇄 갱신 대상 프로퍼티를 지정합니다.
        /// </summary>
        public PropertyChangedForAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
}
