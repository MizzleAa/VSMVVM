using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// 반복 실행 노드. 매 In 발화마다 내부 카운터를 증가시키며 Body 를 발화.
    /// 카운터가 Count 에 도달하면 Body 대신 Done 을 발화하고 카운터 초기화.
    /// <para>
    /// 사용 패턴:
    ///   <c>Start → Repeat.In → Body → (반복 로직) → Repeat.In</c> (백엣지) 로 배선.
    ///   Repeat 이 Count 번 Body 를 발화한 뒤 Done 으로 빠져나감.
    /// </para>
    /// <para>
    /// Index 데이터 출력은 0-based 현재 반복 인덱스. 매 Body 발화 직전에 갱신.
    /// </para>
    /// </summary>
    public sealed class RepeatNode : NodeBase
    {
        public const string TypeIdConst = "Core.Repeat";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",    "In",    PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Count", "Count", PinDirection.Input,  PinKind.Data, typeof(int),  0),
            new PinDescriptor("Body",  "Body",  PinDirection.Output, PinKind.Exec, typeof(void), null),
            new PinDescriptor("Done",  "Done",  PinDirection.Output, PinKind.Exec, typeof(void), null),
            new PinDescriptor("Index", "Index", PinDirection.Output, PinKind.Data, typeof(int),  0),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        /// <summary>현재까지 발화한 Body 횟수 (0-based 다음 인덱스). 실행 인스턴스별 상태.</summary>
        private int _current;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var count = context.GetInput<int>(this, "Count");
            if (_current >= count)
            {
                _current = 0; // 다음 Run 을 위해 초기화
                return Task.FromResult(ExecutionFlow.Continue("Done"));
            }

            // 매 반복마다 pull-eval 캐시를 비워야 Body 안의 GetVariable 등이 최신 값을 다시 계산한다.
            // 클리어 후 Index 를 다시 세팅.
            context.InvalidateDataCache();
            context.SetOutput<int>(this, "Index", _current);
            _current++;
            return Task.FromResult(ExecutionFlow.Continue("Body"));
        }

        public override void WriteState(Utf8JsonWriter writer)
        {
            // 반복 상태는 실행 시점의 값이라 저장 불필요 — 그래프 로드 후엔 0에서 시작.
        }

        public override void ReadState(JsonElement state)
        {
            _current = 0;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Repeat", "Flow",
            "Fires Body exec-out Count times, then Done. Wire Body → work → Repeat.In to loop.",
            0,
            typeof(RepeatNode), () => new RepeatNode(), PinSpec);
    }
}
