using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// I.5 — Value 가 [Min, Max] 범위를 벗어나면 AssertionFailedException. inclusive 양끝.
    /// </summary>
    public sealed class RangeAssertNode : NodeBase
    {
        public const string TypeIdConst = "Core.RangeAssert";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",    "In",    PinDirection.Input,  PinKind.Exec, typeof(void),   null),
            new PinDescriptor("Value", "Value", PinDirection.Input,  PinKind.Data, typeof(double), 0.0),
            new PinDescriptor("Min",   "Min",   PinDirection.Input,  PinKind.Data, typeof(double), double.MinValue),
            new PinDescriptor("Max",   "Max",   PinDirection.Input,  PinKind.Data, typeof(double), double.MaxValue),
            new PinDescriptor("Then",  "Then",  PinDirection.Output, PinKind.Exec, typeof(void),   null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var v = context.GetInput<double>(this, "Value");
            var min = context.GetInput<double>(this, "Min");
            var max = context.GetInput<double>(this, "Max");
            if (v < min || v > max)
            {
                var msg = string.Format(CultureInfo.InvariantCulture,
                    "Value {0} is outside [{1}, {2}].", v, min, max);
                throw new AssertionFailedException(Id, msg);
            }
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "RangeAssert", "Diagnostics",
            "Throws AssertionFailedException if Value is outside [Min, Max].", 0,
            typeof(RangeAssertNode), () => new RangeAssertNode(), PinSpec);
    }
}
