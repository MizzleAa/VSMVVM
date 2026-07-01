using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// 호출(exec)마다 내부 bool 상태를 뒤집고 Then 발화. 현재 상태가 'Value' 출력으로 흐른다.
    /// 첫 호출 시 InitialValue(기본 false)에서 시작 — 첫 실행에서 true → 그 다음 false → ... .
    /// 상태는 그래프 인스턴스에 유지되며, JSON 직렬화 시 함께 저장된다 (WriteState/ReadState).
    /// </summary>
    public sealed class ToggleNode : NodeBase
    {
        public const string TypeIdConst = "Core.Toggle";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",           "In",           PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("InitialValue", "InitialValue", PinDirection.Input,  PinKind.Data, typeof(bool), false),
            new PinDescriptor("Then",         "Then",         PinDirection.Output, PinKind.Exec, typeof(void), null),
            new PinDescriptor("Value",        "Value",        PinDirection.Output, PinKind.Data, typeof(bool), false),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        private bool _state;
        private bool _initialized;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            if (!_initialized)
            {
                _state = context.GetInput<bool>(this, "InitialValue");
                _initialized = true;
            }
            // 호출마다 뒤집기
            _state = !_state;
            context.SetOutput(this, "Value", _state);
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        public override void WriteState(Utf8JsonWriter writer)
        {
            writer.WriteBoolean("state", _state);
            writer.WriteBoolean("initialized", _initialized);
        }

        public override void ReadState(JsonElement state)
        {
            if (state.ValueKind != JsonValueKind.Object) return;
            if (state.TryGetProperty("state", out var s) && s.ValueKind == JsonValueKind.True)
            {
                _state = true;
            }
            else if (state.TryGetProperty("state", out var s2) && s2.ValueKind == JsonValueKind.False)
            {
                _state = false;
            }
            if (state.TryGetProperty("initialized", out var i))
            {
                _initialized = i.ValueKind == JsonValueKind.True;
            }
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Toggle", "Flow",
            "Each exec call flips internal bool state and outputs current value.", 0,
            typeof(ToggleNode), () => new ToggleNode(), PinSpec);
    }
}
