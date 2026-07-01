using System;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.Core.Scheduler.Graph
{
    /// <summary>그래프의 한 연결. 소스 출력 핀 → 타겟 입력 핀.</summary>
    public sealed class NodeConnection : IEquatable<NodeConnection>
    {
        public Guid Id { get; }
        public Guid SourceNodeId { get; }
        public string SourcePinId { get; }
        public Guid TargetNodeId { get; }
        public string TargetPinId { get; }
        public PinKind Kind { get; }

        public NodeConnection(Guid id, Guid sourceNodeId, string sourcePinId,
                              Guid targetNodeId, string targetPinId, PinKind kind)
        {
            Id = id;
            SourceNodeId = sourceNodeId;
            SourcePinId = sourcePinId ?? throw new ArgumentNullException(nameof(sourcePinId));
            TargetNodeId = targetNodeId;
            TargetPinId = targetPinId ?? throw new ArgumentNullException(nameof(targetPinId));
            Kind = kind;
        }

        public bool Equals(NodeConnection other)
        {
            if (other is null) return false;
            return Id == other.Id;
        }

        public override bool Equals(object obj) => Equals(obj as NodeConnection);
        public override int GetHashCode() => Id.GetHashCode();
    }
}
