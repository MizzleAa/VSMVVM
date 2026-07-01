using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// 조건 분기. Condition 데이터 입력값에 따라 True / False exec-out 중 하나만 발화.
    /// </summary>
    public sealed class BranchNode : NodeBase
    {
        public const string TypeIdConst = "Core.Branch";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",        "In",        PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Condition", "Condition", PinDirection.Input,  PinKind.Data, typeof(bool), false),
            new PinDescriptor("True",      "True",      PinDirection.Output, PinKind.Exec, typeof(void), null),
            new PinDescriptor("False",     "False",     PinDirection.Output, PinKind.Exec, typeof(void), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var cond = context.GetInput<bool>(this, "Condition");
            return Task.FromResult(ExecutionFlow.Continue(cond ? "True" : "False"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Branch", "Flow", "Fires True or False exec-out based on Condition.", 0,
            typeof(BranchNode), () => new BranchNode(), PinSpec);
    }
}
