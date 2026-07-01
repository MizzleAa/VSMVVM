using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.WPF.Sample.Scheduler
{
    /// <summary>
    /// "ImageView" 모니터 노드 — Mat 데이터 입력을 받으면 ImageViewMessage 를 Messenger 로 발화.
    /// 사용자가 ViewName(라벨)을 지정하고, 대응되는 ImageViewWindow 가 같은 ViewName 으로 구독.
    /// Exec 흐름 통과(In → Then) 이므로 기존 그래프에 끼워넣기 가능 (OpenCV imshow 와 비슷한 위치).
    /// Sample 전용 — OpenCvSharp 는 Sample 어셈블리에만 의존하며, 본 노드도 Sample 안에 위치.
    /// </summary>
    public sealed class ImageViewNode : NodeBase
    {
        public const string TypeIdConst = "Sample.ImageView";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",    "In",    PinDirection.Input,  PinKind.Exec, typeof(void),            null),
            new PinDescriptor("Image", "Image", PinDirection.Input,  PinKind.Data, typeof(OpenCvSharp.Mat), null),
            new PinDescriptor("Then",  "Then",  PinDirection.Output, PinKind.Exec, typeof(void),            null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        /// <summary>다이얼로그 식별자. 사용자가 인스펙터에서 편집. 기본값 "view".</summary>
        public string ViewName { get; set; } = "view";

        /// <summary>ExecutionContext.Variables 에서 ImageSnapshotStore 를 꺼내는 키.</summary>
        public const string SnapshotStoreKey = "__ImageSnapshotStore";

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var img = context.GetInput<OpenCvSharp.Mat>(this, "Image");
            // 메시지 발화 — 추후 실시간 push 용도 (현재는 다이얼로그가 직접 구독하지 않음).
            context.Messenger?.Send(new ImageViewMessage(ViewName, img));

            // 스냅샷 보관 — Mat.Clone() 으로 deep copy 후 store 에 push.
            if (img != null && !img.Empty()
                && context.Variables.TryGetValue(SnapshotStoreKey, out var raw)
                && raw is ImageSnapshotStore store)
            {
                store.Push(ViewName, img.Clone());
                store.RaiseChanged(ViewName);
            }
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        public override void WriteState(Utf8JsonWriter writer)
        {
            if (ViewName != null) writer.WriteString("viewName", ViewName);
        }

        public override void ReadState(JsonElement state)
        {
            if (state.ValueKind == JsonValueKind.Object &&
                state.TryGetProperty("viewName", out var n) && n.ValueKind == JsonValueKind.String)
            {
                ViewName = n.GetString();
            }
        }

        public static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "ImageView", "OpenCV",
            "Sends the incoming Mat to an ImageViewWindow tagged with ViewName.", 0,
            typeof(ImageViewNode), () => new ImageViewNode(), PinSpec);
    }
}
