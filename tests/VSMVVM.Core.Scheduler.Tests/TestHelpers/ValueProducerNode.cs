using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Tests.TestHelpers
{
    /// <summary>
    /// 데이터 풀 테스트용 노드. ExecuteAsync/EvaluateAsync가 호출되면 미리 보관한 값을
    /// "Value" 출력 핀에 SetOutput. 호출 횟수도 카운트하여 캐싱 검증 가능.
    /// </summary>
    public sealed class ValueProducerNode<T> : NodeBase
    {
        private readonly T _value;
        public int EvaluatedCount { get; private set; }

        public ValueProducerNode(T value) { _value = value; }

        public override string TypeId => "Test.ValueProducer";

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => new[]
        {
            new PinDescriptor("Value", "Value", PinDirection.Output, PinKind.Data, typeof(T), default(T)),
        };

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            EvaluatedCount++;
            context.SetOutput(this, "Value", _value);
            return Task.FromResult(ExecutionFlow.Halt);
        }
    }

    /// <summary>
    /// pull 의존성 사이클 테스트용. exec-in으로 진입하여 Source 데이터 입력을 풀하는데,
    /// 그래프가 Source ← Echo (자기 출력)로 연결되어 있으면 풀이 자기 자신을 부르게 되어 사이클.
    /// </summary>
    public sealed class CyclicDriverNode : NodeBase
    {
        public override string TypeId => "Test.CyclicDriver";

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => new[]
        {
            new PinDescriptor("In",     "In",     PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Source", "Source", PinDirection.Input,  PinKind.Data, typeof(int),  0),
            new PinDescriptor("Echo",   "Echo",   PinDirection.Output, PinKind.Data, typeof(int),  0),
            new PinDescriptor("Then",   "Then",   PinDirection.Output, PinKind.Exec, typeof(void), null),
        };

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var v = context.GetInput<int>(this, "Source"); // 이 호출이 cycle 트리거
            context.SetOutput(this, "Echo", v);
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }
    }
}
