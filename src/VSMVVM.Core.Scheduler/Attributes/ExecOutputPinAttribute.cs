using System;

namespace VSMVVM.Core.Scheduler.Attributes
{
    /// <summary>
    /// Exec 출력 핀(다음 노드로의 제어 흐름)을 선언합니다. Blueprint의 흰색 출력 핀에 해당합니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ExecOutputPinAttribute : Attribute
    {
        public string DisplayName { get; set; }
    }
}
