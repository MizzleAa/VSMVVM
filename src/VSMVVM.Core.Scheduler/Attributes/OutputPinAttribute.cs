using System;

namespace VSMVVM.Core.Scheduler.Attributes
{
    /// <summary>
    /// 데이터 출력 핀(노드가 산출하여 하류 노드로 공급하는 값)을 선언합니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class OutputPinAttribute : Attribute
    {
        public string DisplayName { get; set; }
    }
}
