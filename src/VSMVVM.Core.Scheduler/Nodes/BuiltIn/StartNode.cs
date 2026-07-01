using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>그래프 진입점. exec-in 없음, exec-out "Then" 1개.</summary>
    public sealed class StartNode : NodeBase
    {
        public const string TypeIdConst = "Core.Start";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
            => Task.FromResult(ExecutionFlow.Continue("Then"));

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Start", "Flow", "Entry point of a graph.", 0,
            typeof(StartNode), () => new StartNode(), PinSpec);
    }
}
