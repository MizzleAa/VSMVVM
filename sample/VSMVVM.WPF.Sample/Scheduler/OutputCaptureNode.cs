using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.WPF.Sample.Scheduler
{
    /// <summary>
    /// 샘플 데모 전용 노드. 데이터 입력 'Value'를 ExecutionContext.Variables["__Outputs"] (List&lt;int&gt;)에 누적하고
    /// 'Then' 흐름을 이어간다. Phase I.2c의 OutputNode가 도입되면 대체될 임시 도구.
    /// </summary>
    public sealed class OutputCaptureNode : NodeBase
    {
        public const string TypeIdConst = "Sample.OutputCapture";
        public override string TypeId => TypeIdConst;

        public const string VariablesKey = "__Outputs";

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",    "In",    PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Value", "Value", PinDirection.Input,  PinKind.Data, typeof(int),  0),
            new PinDescriptor("Then",  "Then",  PinDirection.Output, PinKind.Exec, typeof(void), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var v = context.GetInput<int>(this, "Value");
            if (!context.Variables.TryGetValue(VariablesKey, out var existing) || existing is not List<int> list)
            {
                list = new List<int>();
                context.Variables[VariablesKey] = list;
            }
            list.Add(v);
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        public static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Output Capture", "Sample", "Captures the Value into ExecutionContext.Variables for demo verification.",
            0, typeof(OutputCaptureNode), () => new OutputCaptureNode(), PinSpec);
    }
}
