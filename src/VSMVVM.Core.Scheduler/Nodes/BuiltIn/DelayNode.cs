using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>지정된 초만큼 비동기 대기 후 Then 발화. CancellationToken 존중.</summary>
    public sealed class DelayNode : NodeBase
    {
        public const string TypeIdConst = "Core.Delay";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",      "In",      PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Seconds", "Seconds", PinDirection.Input,  PinKind.Data, typeof(double), 0.0),
            new PinDescriptor("Then",    "Then",    PinDirection.Output, PinKind.Exec, typeof(void), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override async Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var seconds = context.GetInput<double>(this, "Seconds");
            if (seconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), context.CancellationToken).ConfigureAwait(false);
            }
            return ExecutionFlow.Continue("Then");
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Delay", "Flow", "Asynchronously waits N seconds.", 0,
            typeof(DelayNode), () => new DelayNode(), PinSpec);
    }
}
