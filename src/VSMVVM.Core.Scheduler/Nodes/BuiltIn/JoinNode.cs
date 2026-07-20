using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// 여러 exec-in <c>In0..InN-1</c> 도착을 카운트해 <b>마지막</b> 도달자만 <c>Then</c> 을 발화한다.
    /// <see cref="ForkNode"/> 로 시작된 병렬 브랜치들의 재합류 지점.
    /// <para>
    /// SchedulerService 는 <see cref="IJoinBarrierNode"/> marker 를 감지해 카운팅 · gating 을 수행하므로
    /// 노드의 <see cref="ExecuteAsync"/> 는 카운트 관리 없이 <see cref="ExecutionFlow.Continue(string[])"/> 만 반환.
    /// 대신 노드가 스스로 발화 여부를 판단할 수 있게 SchedulerService 는 <c>context.Variables</c> 를 통해
    /// arrival 상태를 전달한다 — 자세한 규약은 SchedulerService 참고.
    /// </para>
    /// </summary>
    public sealed class JoinNode : NodeBase, IDynamicPinCountNode, IJoinBarrierNode
    {
        public const string TypeIdConst = "Core.Join";
        public override string TypeId => TypeIdConst;

        internal const int DefaultInputCount = 2;
        internal const int MinInputCountValue = 2;
        internal const int MaxInputCountValue = 16;

        private int _inputCount = DefaultInputCount;

        /// <summary>대기할 in-핀 개수 (2 ~ 16). setter 는 range clamp 하고 InvalidatePins.</summary>
        public int InputCount
        {
            get => _inputCount;
            set
            {
                if (value < MinInputCountValue) value = MinInputCountValue;
                if (value > MaxInputCountValue) value = MaxInputCountValue;
                if (_inputCount == value) return;
                _inputCount = value;
                InvalidatePins();
            }
        }

        int IDynamicPinCountNode.DynamicPinCount
        {
            get => InputCount;
            set => InputCount = value;
        }
        string IDynamicPinCountNode.DynamicPinCountLabel => "Inputs";
        int IDynamicPinCountNode.MinDynamicPinCount => MinInputCountValue;
        int IDynamicPinCountNode.MaxDynamicPinCount => MaxInputCountValue;

        int IJoinBarrierNode.ExpectedArrivals => _inputCount;

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors()
        {
            var list = new List<PinDescriptor>(_inputCount + 1);
            for (int i = 0; i < _inputCount; i++)
            {
                list.Add(new PinDescriptor($"In{i}", $"In {i}", PinDirection.Input, PinKind.Exec, typeof(void), null));
            }
            list.Add(new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void), null));
            return list;
        }

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            // 실제 gating (마지막 도달자만 통과) 은 SchedulerService 가 IJoinBarrierNode 감지로 처리.
            // 여기서는 노드가 도달했다는 사실만 표시하고 Then 을 발화 — Scheduler 가 필요 시 Halt 로 override.
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        public override void WriteState(Utf8JsonWriter writer)
        {
            writer.WriteNumber("inputCount", _inputCount);
        }

        public override void ReadState(JsonElement state)
        {
            if (state.ValueKind != JsonValueKind.Object) return;
            if (state.TryGetProperty("inputCount", out var ic) && ic.ValueKind == JsonValueKind.Number)
            {
                InputCount = ic.GetInt32();
            }
        }

        internal static readonly PinDescriptor[] DefaultPinSpec = new[]
        {
            new PinDescriptor("In0",  "In 0",  PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("In1",  "In 1",  PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Then", "Then",  PinDirection.Output, PinKind.Exec, typeof(void), null),
        };

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Join", "Flow",
            "여러 In 이 모두 도착한 뒤에만 Then 발화. ForkNode 병렬 브랜치의 재합류 지점.",
            0, typeof(JoinNode), () => new JoinNode(), DefaultPinSpec);
    }
}