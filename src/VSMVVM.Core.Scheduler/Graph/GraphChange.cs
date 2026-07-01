using System;

namespace VSMVVM.Core.Scheduler.Graph
{
    /// <summary>NodeGraph.Changed 이벤트로 전달되는 변경 유형의 베이스.</summary>
    public abstract class GraphChange
    {
    }

    public sealed class NodeAdded : GraphChange
    {
        public Guid NodeId { get; }
        public string TypeId { get; }
        public NodeLayout Layout { get; }

        public NodeAdded(Guid nodeId, string typeId, NodeLayout layout)
        {
            NodeId = nodeId;
            TypeId = typeId;
            Layout = layout;
        }
    }

    public sealed class NodeRemoved : GraphChange
    {
        public Guid NodeId { get; }
        public NodeRemoved(Guid nodeId) { NodeId = nodeId; }
    }

    public sealed class Connected : GraphChange
    {
        public NodeConnection Connection { get; }
        public Connected(NodeConnection connection) { Connection = connection; }
    }

    public sealed class Disconnected : GraphChange
    {
        public Guid ConnectionId { get; }
        public Disconnected(Guid connectionId) { ConnectionId = connectionId; }
    }

    public sealed class Moved : GraphChange
    {
        public Guid NodeId { get; }
        public NodeLayout From { get; }
        public NodeLayout To { get; }

        public Moved(Guid nodeId, NodeLayout from, NodeLayout to)
        {
            NodeId = nodeId;
            From = from;
            To = to;
        }
    }
}
