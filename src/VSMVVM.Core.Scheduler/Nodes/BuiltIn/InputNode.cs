using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// Phase J — 그래프 외부 입력을 끌어오는 노드. <see cref="OutputNode"/> 의 대칭.
    /// <para>
    /// 외부 호출자가 RunAsync 전에 <c>ctx.Inputs[Key] = value</c> 로 채워두면, 본 노드의 Value 출력 핀이
    /// 그 값을 노출. 키가 없으면 Default 핀의 값을 반환. 그래프를 함수처럼 호출할 때 사용.
    /// </para>
    /// </summary>
    public sealed class InputNode : NodeBase
    {
        public const string TypeIdConst = "Core.Input";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",      "In",      PinDirection.Input,  PinKind.Exec, typeof(void),   null),
            new PinDescriptor("Key",     "Key",     PinDirection.Input,  PinKind.Data, typeof(string), ""),
            new PinDescriptor("Default", "Default", PinDirection.Input,  PinKind.Data, typeof(object), null),
            new PinDescriptor("Value",   "Value",   PinDirection.Output, PinKind.Data, typeof(object), null),
            new PinDescriptor("Then",    "Then",    PinDirection.Output, PinKind.Exec, typeof(void),   null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var key = context.GetInput<string>(this, "Key") ?? string.Empty;
            object value;
            if (context.Inputs.TryGetValue(key, out var external))
            {
                value = external;
            }
            else
            {
                value = context.GetInput<object>(this, "Default");
            }
            context.SetOutput(this, "Value", value);
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Input", "IO",
            "Reads a value from ExecutionContext.Inputs[Key] (or Default if missing).", 0,
            typeof(InputNode), () => new InputNode(), PinSpec);
    }
}
