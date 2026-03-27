using System;

namespace VSMVVM.Core.Attributes
{
    /// <summary>
    /// 필드에 적용하면 Source Generator가 자동으로 public 프로퍼티를 생성합니다.
    /// 필드 이름은 반드시 '_' 접두사로 시작해야 합니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PropertyAttribute : Attribute
    {
    }
}
