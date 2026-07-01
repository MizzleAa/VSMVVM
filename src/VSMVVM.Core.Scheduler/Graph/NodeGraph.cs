using System;
using System.Collections.Generic;
using System.Linq;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.Core.Scheduler.Graph
{
    /// <summary>
    /// Blueprint 스타일 노드 그래프. 노드 + 연결 + 레이아웃을 보유하며 변경 이벤트를 발화합니다.
    /// 실행은 SchedulerService(Phase 3a)가 담당; 본 클래스는 데이터 모델만.
    /// </summary>
    public sealed class NodeGraph
    {
        private readonly Dictionary<Guid, INode> _nodes = new Dictionary<Guid, INode>();
        private readonly Dictionary<Guid, NodeLayout> _layouts = new Dictionary<Guid, NodeLayout>();
        private readonly List<NodeConnection> _connections = new List<NodeConnection>();
        private readonly Dictionary<string, GraphVariable> _variables = new Dictionary<string, GraphVariable>(StringComparer.Ordinal);

        public Guid Id { get; }
        public string Name { get; set; }

        public IReadOnlyCollection<INode> Nodes => _nodes.Values;
        public IReadOnlyList<NodeConnection> Connections => _connections;
        public IReadOnlyDictionary<Guid, NodeLayout> Layouts => _layouts;

        /// <summary>그래프 단위 변수 저장소. Get/SetVariableNode 가 참조.</summary>
        public IReadOnlyDictionary<string, GraphVariable> Variables => _variables;

        /// <summary>
        /// 호스트 자유 메타데이터 — JSON 라운드트립 시 같이 보존/복원. Core 는 키 의미를 모름.
        /// 예: Sample 이 "userCode" 키에 사용자 C# 소스 보관 → Save/Load 한 파일로 끝.
        /// </summary>
        public Dictionary<string, System.Text.Json.JsonElement> Extras { get; }
            = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);

        /// <summary>그래프 변경 이벤트. 에디터 ViewModel 동기화용.</summary>
        public event Action<NodeGraph, GraphChange> Changed;

        /// <summary>변수 추가/제거 시 발화. UI(팔레트 동적 갱신 등) 가 구독.</summary>
        public event EventHandler VariablesChanged;

        /// <summary>새 변수 등록. 이름 중복 시 InvalidOperationException.</summary>
        public GraphVariable AddVariable(string name, Type clrType, object defaultValue)
        {
            var v = new GraphVariable(name, clrType, defaultValue);
            if (_variables.ContainsKey(v.Name))
            {
                throw new InvalidOperationException($"Variable '{v.Name}' already exists in this graph.");
            }
            _variables[v.Name] = v;
            VariablesChanged?.Invoke(this, EventArgs.Empty);
            return v;
        }

        /// <summary>변수 제거. 존재했으면 true.</summary>
        public bool RemoveVariable(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (!_variables.Remove(name)) return false;
            VariablesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public NodeGraph()
        {
            Id = Guid.NewGuid();
        }

        public NodeGraph(Guid id)
        {
            Id = id;
        }

        public INode GetNode(Guid id) => _nodes.TryGetValue(id, out var n) ? n : null;

        /// <summary>
        /// NodeMetadataRegistry에서 typeId로 노드 팩토리를 찾아 새 인스턴스를 그래프에 추가합니다.
        /// </summary>
        public INode AddNode(string typeId, double x, double y)
        {
            if (typeId == null) throw new ArgumentNullException(nameof(typeId));
            BuiltInNodes.EnsureRegistered();
            var meta = NodeMetadataRegistry.Get(typeId)
                ?? throw new InvalidOperationException($"Unknown node type id '{typeId}'.");
            var node = meta.Factory();
            return AddNode(node, x, y);
        }

        /// <summary>
        /// 외부에서 생성한 노드 인스턴스를 그래프에 추가합니다. 테스트/PoC용.
        /// </summary>
        public INode AddNode(INode node, double x, double y)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (_nodes.ContainsKey(node.Id))
            {
                throw new InvalidOperationException($"Node {node.Id} is already in the graph.");
            }

            var layout = new NodeLayout(x, y);
            _nodes[node.Id] = node;
            _layouts[node.Id] = layout;
            Changed?.Invoke(this, new NodeAdded(node.Id, node.TypeId, layout));
            return node;
        }

        /// <summary>
        /// 노드 제거. 연결된 모든 NodeConnection도 함께 제거됩니다.
        /// </summary>
        public bool RemoveNode(Guid nodeId)
        {
            if (!_nodes.Remove(nodeId))
            {
                return false;
            }
            _layouts.Remove(nodeId);

            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                var c = _connections[i];
                if (c.SourceNodeId == nodeId || c.TargetNodeId == nodeId)
                {
                    _connections.RemoveAt(i);
                    Changed?.Invoke(this, new Disconnected(c.Id));
                }
            }

            Changed?.Invoke(this, new NodeRemoved(nodeId));
            return true;
        }

        /// <summary>
        /// 두 핀을 연결합니다. N:M 규칙(ConnectionRules) 적용:
        ///   - data-input 핀에 기존 연결이 있으면 자동 disconnect 후 신규 연결.
        ///   - 그 외(Exec, data-output)는 무제한.
        /// 호환되지 않는 핀이면 InvalidOperationException.
        /// </summary>
        public NodeConnection Connect(Guid srcNodeId, string srcPinId, Guid dstNodeId, string dstPinId)
        {
            var srcNode = GetNode(srcNodeId)
                ?? throw new InvalidOperationException($"Source node {srcNodeId} not in graph.");
            var dstNode = GetNode(dstNodeId)
                ?? throw new InvalidOperationException($"Target node {dstNodeId} not in graph.");

            var srcPin = FindPin(srcNode, srcPinId)
                ?? throw new InvalidOperationException($"Source pin '{srcPinId}' not on node {srcNodeId}.");
            var dstPin = FindPin(dstNode, dstPinId)
                ?? throw new InvalidOperationException($"Target pin '{dstPinId}' not on node {dstNodeId}.");

            if (!ConnectionRules.CanConnect(_connections, srcPin, dstPin, out var reason, out var conflicts))
            {
                throw new InvalidOperationException($"Cannot connect: {reason}");
            }

            // 자동 disconnect (data-input 1:1 규칙)
            if (conflicts.Count > 0)
            {
                foreach (var c in conflicts)
                {
                    _connections.Remove(c);
                    Changed?.Invoke(this, new Disconnected(c.Id));
                }
            }

            var connection = new NodeConnection(Guid.NewGuid(), srcNodeId, srcPinId, dstNodeId, dstPinId, srcPin.Kind);
            _connections.Add(connection);
            Changed?.Invoke(this, new Connected(connection));
            return connection;
        }

        public bool Disconnect(Guid connectionId)
        {
            var idx = _connections.FindIndex(c => c.Id == connectionId);
            if (idx < 0) return false;
            _connections.RemoveAt(idx);
            Changed?.Invoke(this, new Disconnected(connectionId));
            return true;
        }

        public void MoveNode(Guid nodeId, double x, double y)
        {
            if (!_layouts.TryGetValue(nodeId, out var from))
            {
                throw new InvalidOperationException($"Node {nodeId} not in graph.");
            }
            var to = new NodeLayout(x, y);
            _layouts[nodeId] = to;
            Changed?.Invoke(this, new Moved(nodeId, from, to));
        }

        private static IPin FindPin(INode node, string pinId)
        {
            for (int i = 0; i < node.Pins.Count; i++)
            {
                if (node.Pins[i].Id == pinId) return node.Pins[i];
            }
            return null;
        }
    }
}
