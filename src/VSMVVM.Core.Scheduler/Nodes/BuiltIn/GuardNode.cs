using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// I.5 — Branch 와 비슷하지만 의도를 "가드" 로 명확화한 노드. 조건이 true 면 Pass, false 면 Fail 분기.
    /// 예외를 던지지 않는다 — 단순 분기.
    /// </summary>
    public sealed class GuardNode : NodeBase
    {
        public const string TypeIdConst = "Core.Guard";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",        "In",        PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Condition", "Condition", PinDirection.Input,  PinKind.Data, typeof(bool), false),
            new PinDescriptor("Pass",      "Pass",      PinDirection.Output, PinKind.Exec, typeof(void), null),
            new PinDescriptor("Fail",      "Fail",      PinDirection.Output, PinKind.Exec, typeof(void), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var cond = context.GetInput<bool>(this, "Condition");
            return Task.FromResult(ExecutionFlow.Continue(cond ? "Pass" : "Fail"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Guard", "Diagnostics",
            "Branches into Pass/Fail based on Condition (no throw).", 0,
            typeof(GuardNode), () => new GuardNode(), PinSpec);
    }
}
