using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// exec-in 1 개 + exec-out <c>Branch0..BranchN-1</c> 을 병렬 Task 로 분기 실행한다.
    /// SchedulerService 는 <see cref="IParallelForkNode"/> marker 를 감지해 각 브랜치를 <c>Task.Run</c> 으로 실행 후
    /// 최종 <see cref="JoinNode"/> 에서 재합류한다.
    /// <para>
    /// <see cref="BranchCount"/> 로 분기 수 가변 (기본 2, 2~16). 변경 시 <see cref="NodeBase.InvalidatePins"/> 로 핀 즉시 재생성.
    /// </para>
    /// </summary>
    public sealed class ForkNode : NodeBase, IDynamicPinCountNode, IParallelForkNode
    {
        public const string TypeIdConst = "Core.Fork";
        public override string TypeId => TypeIdConst;

        internal const int DefaultBranchCount = 2;
        internal const int MinBranchCountValue = 2;
        internal const int MaxBranchCountValue = 16;

        private int _branchCount = DefaultBranchCount;

        /// <summary>분기 개수 (2 ~ 16). setter 는 range clamp 하고 InvalidatePins.</summary>
        public int BranchCount
        {
            get => _branchCount;
            set
            {
                if (value < MinBranchCountValue) value = MinBranchCountValue;
                if (value > MaxBranchCountValue) value = MaxBranchCountValue;
                if (_branchCount == value) return;
                _branchCount = value;
                InvalidatePins();
            }
        }

        int IDynamicPinCountNode.DynamicPinCount
        {
            get => BranchCount;
            set => BranchCount = value;
        }
        string IDynamicPinCountNode.DynamicPinCountLabel => "Branches";
        int IDynamicPinCountNode.MinDynamicPinCount => MinBranchCountValue;
        int IDynamicPinCountNode.MaxDynamicPinCount => MaxBranchCountValue;

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors()
        {
            var list = new List<PinDescriptor>(1 + _branchCount)
            {
                new PinDescriptor("In", "In", PinDirection.Input, PinKind.Exec, typeof(void), null),
            };
            for (int i = 0; i < _branchCount; i++)
            {
                list.Add(new PinDescriptor($"Branch{i}", $"Branch {i}", PinDirection.Output, PinKind.Exec, typeof(void), null));
            }
            return list;
        }

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var pins = new string[_branchCount];
            for (int i = 0; i < _branchCount; i++)
            {
                pins[i] = $"Branch{i}";
            }
            return Task.FromResult(ExecutionFlow.Continue(pins));
        }

        public override void WriteState(Utf8JsonWriter writer)
        {
            writer.WriteNumber("branchCount", _branchCount);
        }

        public override void ReadState(JsonElement state)
        {
            if (state.ValueKind != JsonValueKind.Object) return;
            if (state.TryGetProperty("branchCount", out var bc) && bc.ValueKind == JsonValueKind.Number)
            {
                BranchCount = bc.GetInt32();
            }
        }

        internal static readonly PinDescriptor[] DefaultPinSpec = new[]
        {
            new PinDescriptor("In",      "In",       PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Branch0", "Branch 0", PinDirection.Output, PinKind.Exec, typeof(void), null),
            new PinDescriptor("Branch1", "Branch 1", PinDirection.Output, PinKind.Exec, typeof(void), null),
        };

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Fork", "Flow",
            "각 Branch 를 병렬 Task 로 분기 실행. JoinNode 로 재합류.",
            0, typeof(ForkNode), () => new ForkNode(), DefaultPinSpec);
    }
}