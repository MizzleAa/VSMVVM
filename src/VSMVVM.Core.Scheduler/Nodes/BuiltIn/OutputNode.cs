using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// I.2c — 그래프 외부로 "결과"를 노출하는 노드. ExecutionContext.Outputs[Key] = Value 를 채운다.
    /// 외부 호출자는 ExecutionResult.Outputs 로 받아 그래프를 함수처럼 사용 가능.
    /// 동일 키 재기록 시 마지막 값이 유효.
    /// </summary>
    public sealed class OutputNode : NodeBase
    {
        public const string TypeIdConst = "Core.Output";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",    "In",    PinDirection.Input,  PinKind.Exec, typeof(void),   null),
            new PinDescriptor("Key",   "Key",   PinDirection.Input,  PinKind.Data, typeof(string), "result"),
            new PinDescriptor("Value", "Value", PinDirection.Input,  PinKind.Data, typeof(object), null),
            new PinDescriptor("Then",  "Then",  PinDirection.Output, PinKind.Exec, typeof(void),   null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var key = context.GetInput<string>(this, "Key");
            if (string.IsNullOrEmpty(key)) key = "result";
            var value = context.GetInput<object>(this, "Value");
            context.Outputs[key] = value;
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Output", "IO", "Writes a key/value pair to ExecutionResult.Outputs.", 0,
            typeof(OutputNode), () => new OutputNode(), PinSpec);
    }
}
