using System;

namespace VSMVVM.Core.Scheduler.Attributes
{
    /// <summary>
    /// Exec 입력 핀(제어 흐름 진입점)을 선언합니다. Blueprint의 흰색 입력 핀에 해당합니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ExecInputPinAttribute : Attribute
    {
        public string DisplayName { get; set; }
    }
}
