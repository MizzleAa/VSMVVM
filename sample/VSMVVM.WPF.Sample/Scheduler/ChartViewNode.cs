using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.WPF.Sample.Scheduler
{
    public enum ChartKind
    {
        Line,
        ConfusionMatrix,
        Heatmap,
    }

    /// <summary>
    /// "ChartView" 앵커 노드 — 그래프에서 실행 흐름(In→Then)을 통과할 뿐, 데이터 push 는 사용자 스니펫이
    /// <see cref="ChartLog"/> 를 통해 수행. 노드 자체는 창이 열릴 대상(ViewName + Kind)만 나타냄.
    /// ImageViewNode 와 동일 패턴: 노드 더블클릭 → SampleWorkspaceViewModel.OpenChartViewForNode → ChartViewWindow.
    /// </summary>
    public sealed class ChartViewNode : NodeBase
    {
        public const string TypeIdConst = "Sample.ChartView";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",   "In",   PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        /// <summary>다이얼로그 식별자. 사용자 스니펫이 ChartLog.Push(viewName, ...) 로 호출하는 문자열과 매칭.</summary>
        public string ViewName { get; set; } = "chart";

        /// <summary>차트 유형 — Line (시계열 곡선) 또는 ConfusionMatrix (히트맵).</summary>
        public ChartKind Kind { get; set; } = ChartKind.Line;

        /// <summary>ExecutionContext.Variables 에서 ChartSnapshotStore 를 꺼내는 키.</summary>
        public const string SnapshotStoreKey = "__ChartSnapshotStore";

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        public override void WriteState(Utf8JsonWriter writer)
        {
            if (ViewName != null) writer.WriteString("viewName", ViewName);
            writer.WriteString("kind", Kind.ToString());
        }

        public override void ReadState(JsonElement state)
        {
            if (state.ValueKind != JsonValueKind.Object) return;
            if (state.TryGetProperty("viewName", out var n) && n.ValueKind == JsonValueKind.String)
            {
                ViewName = n.GetString();
            }
            if (state.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String
                && System.Enum.TryParse<ChartKind>(k.GetString(), out var parsed))
            {
                Kind = parsed;
            }
        }

        public static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "ChartView", "Charts",
            "Anchor node for a real-time chart window. User snippets call ChartLog.Push(ViewName, ...) to stream data.",
            0,
            typeof(ChartViewNode), () => new ChartViewNode(), PinSpec);
    }
}
