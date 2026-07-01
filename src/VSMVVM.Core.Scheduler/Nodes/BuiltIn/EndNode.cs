using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>그래프 흐름 종단. exec-in "In" 1개, exec-out 없음 → Halt 반환.</summary>
    public sealed class EndNode : NodeBase
    {
        public const string TypeIdConst = "Core.End";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In", "In", PinDirection.Input, PinKind.Exec, typeof(void), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
            => Task.FromResult(ExecutionFlow.Halt);

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "End", "Flow", "Halts the current execution flow.", 0,
            typeof(EndNode), () => new EndNode(), PinSpec);
    }
}
