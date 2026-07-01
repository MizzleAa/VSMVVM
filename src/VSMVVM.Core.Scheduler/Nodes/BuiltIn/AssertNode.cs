using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// I.5 — 조건이 false 면 AssertionFailedException 을 던져 그래프 실행을 중단시킨다.
    /// Condition 입력이 true 면 Then 발화. 디버깅/회귀 방지용.
    /// </summary>
    public sealed class AssertNode : NodeBase
    {
        public const string TypeIdConst = "Core.Assert";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",        "In",        PinDirection.Input,  PinKind.Exec, typeof(void),   null),
            new PinDescriptor("Condition", "Condition", PinDirection.Input,  PinKind.Data, typeof(bool),   true),
            new PinDescriptor("Message",   "Message",   PinDirection.Input,  PinKind.Data, typeof(string), "Assertion failed."),
            new PinDescriptor("Then",      "Then",      PinDirection.Output, PinKind.Exec, typeof(void),   null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var cond = context.GetInput<bool>(this, "Condition");
            if (!cond)
            {
                var msg = context.GetInput<string>(this, "Message") ?? "Assertion failed.";
                throw new AssertionFailedException(Id, msg);
            }
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Assert", "Diagnostics",
            "Throws AssertionFailedException if Condition is false.", 0,
            typeof(AssertNode), () => new AssertNode(), PinSpec);
    }
}
