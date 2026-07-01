using System;

namespace VSMVVM.Core.Scheduler.Attributes
{
    /// <summary>
    /// 데이터 입력 핀(상류 노드의 출력값을 pull로 받음)을 선언합니다.
    /// DefaultValue는 핀이 미연결일 때 사용되는 리터럴 값입니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class InputPinAttribute : Attribute
    {
        public string DisplayName { get; set; }
        public object DefaultValue { get; set; }
    }
}
