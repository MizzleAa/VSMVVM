using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// 순차 분기 노드. exec-in 1개 + exec-out "Then0".."Then2" 3개.
    /// PoC 단계에선 핀 개수 고정 (3); 추후 가변 핀 지원 검토.
    /// </summary>
    public sealed class SequenceNode : NodeBase
    {
        public const string TypeIdConst = "Core.Sequence";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",    "In",    PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Then0", "Then 0", PinDirection.Output, PinKind.Exec, typeof(void), null),
            new PinDescriptor("Then1", "Then 1", PinDirection.Output, PinKind.Exec, typeof(void), null),
            new PinDescriptor("Then2", "Then 2", PinDirection.Output, PinKind.Exec, typeof(void), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
            => Task.FromResult(ExecutionFlow.Continue("Then0", "Then1", "Then2"));

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Sequence", "Flow", "Fires each Then pin in declared order.", 0,
            typeof(SequenceNode), () => new SequenceNode(), PinSpec);
    }
}
