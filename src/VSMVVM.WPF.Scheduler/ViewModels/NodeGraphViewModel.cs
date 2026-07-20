using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.Core.Scheduler.Serialization;
using VSMVVM.WPF.Services;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// 그래프 전체의 View 모델. NodeGraph.Changed 이벤트를 구독하여 ObservableCollection들을 동기화한다.
    /// 캔버스/에디터가 바인딩할 단일 진입점.
    /// 옵션으로 IUndoRedoService를 주입하면 편집 작업이 자동으로 undo/redo 스택에 기록된다.
    /// 옵션으로 ISchedulerService + IMessenger를 주입하면 RunAsync/Continue/Step/ToggleBreakpoint 커맨드와
    /// 실행 중 노드 하이라이트(IsExecuting) / 일시정지 상태(IsPaused)를 자동 관리한다.
    /// </summary>
    public partial class NodeGraphViewModel : ViewModelBase
    {
        public NodeGraph Model { get; }

        public ObservableCollection<NodeViewModel> Nodes { get; } = new();
        public ObservableCollection<ConnectionViewModel> Connections { get; } = new();

        /// <summary>
        /// 다중 선택 컬렉션 — 박스 선택/Ctrl+클릭/Shift+클릭으로 누적되는 모든 선택 노드.
        /// 1개 선택 시 SelectedNode 도 그 노드를 가리키고, 0/2개 이상이면 SelectedNode 는 null.
        /// (RunCommand 등 단일 진입점이 필요한 명령은 SelectedNode 기준으로 동작 유지.)
        /// </summary>
        public ObservableCollection<NodeViewModel> SelectedNodes { get; } = new();

        [Property]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
        [NotifyCanExecuteChangedFor(nameof(StepOverCommand))]
        [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
        private NodeViewModel _selectedNode;

        /// <summary>현재 선택된 연결. Delete 키로 삭제 가능. 노드/연결은 동시에 하나만 선택된 상태로 유지.</summary>
        [Property]
        private ConnectionViewModel _selectedConnection;

        [Property]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool _isRunning;

        [Property]
        [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
        [NotifyCanExecuteChangedFor(nameof(StepOverCommand))]
        private bool _isPaused;

        private readonly Dictionary<Guid, NodeViewModel> _nodeMap = new();
        private readonly Dictionary<Guid, ConnectionViewModel> _connectionMap = new();
        private readonly IUndoRedoService _undoRedo;
        private readonly ISchedulerService _scheduler;
        private readonly IMessenger _messenger;
        // 생성 시점 (보통 UI 스레드) 의 SynchronizationContext — 백그라운드 스레드에서 도착한
        // 런타임 메시지를 UI 스레드로 마샬링하여 ObservableCollection (LastInputs/LastOutputs) 변경이
        // WPF CollectionView 의 cross-thread 에러를 일으키지 않도록 한다.
        private readonly System.Threading.SynchronizationContext _capturedContext;
        private bool _suppressUndoRecording;
        // RemoveNode가 부산물 Disconnected 이벤트들을 트리거하는 동안 노드 본체 push만 1회로 묶기 위한 플래그
        private bool _suppressDisconnectedPushDuringNodeRemove;

        // 현재 실행 중인 RunAsync의 취소 토큰. Stop 커맨드가 이걸 cancel.
        private CancellationTokenSource _runCts;

        public IUndoRedoService UndoRedo => _undoRedo;
        public ISchedulerService Scheduler => _scheduler;

        /// <summary>
        /// Phase I.3 — 노드별 누적 실행 시간 통계. NodeExitedMessage 수신마다 자동 갱신.
        /// 호스트 앱은 ExecutionHistoryPanel / 핫스팟 강조 등에 활용.
        /// </summary>
        public ProfilingStats Profiling { get; } = new ProfilingStats();

        /// <summary>I.2b — Run 시 ExecutionContext.HistoryStore 로 주입할 보관소(선택).</summary>
        public IExecutionHistoryStore HistoryStore { get; set; }

        /// <summary>I.4 — Run 시 ExecutionContext.LogSink 로 주입할 로그 sink(선택).</summary>
        public ISchedulerLogSink LogSink { get; set; }

        /// <summary>
        /// Run 시작 직전 ExecutionContext 에 호스트가 추가 정보(Variables 의 보관소 등)를 주입할 수 있는 후크.
        /// 예: ImageSnapshotStore 를 ctx.Variables 에 push 해 노드가 접근할 수 있도록.
        /// </summary>
        public System.Action<ExecutionContext> ConfigureContext { get; set; }

        public NodeGraphViewModel(NodeGraph model) : this(model, null, null, null) { }

        public NodeGraphViewModel(NodeGraph model, IUndoRedoService undoRedo)
            : this(model, undoRedo, null, null) { }

        public NodeGraphViewModel(NodeGraph model,
                                  IUndoRedoService undoRedo,
                                  ISchedulerService scheduler,
                                  IMessenger messenger)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _undoRedo = undoRedo;
            _scheduler = scheduler;
            _messenger = messenger;
            _capturedContext = System.Threading.SynchronizationContext.Current;

            // 기존 노드/연결을 ViewModel로 변환
            foreach (var node in Model.Nodes)
            {
                var layout = Model.Layouts.TryGetValue(node.Id, out var l) ? l : new NodeLayout(0, 0);
                AddNodeViewModel(node, layout.X, layout.Y);
            }
            foreach (var conn in Model.Connections)
            {
                AddConnectionViewModel(conn);
            }

            Model.Changed += OnGraphChanged;

            // 런타임 메시지 구독
            if (_messenger != null)
            {
                _messenger.Register<NodeEnteringMessage>(this, OnNodeEntering);
                _messenger.Register<NodeExitedMessage>(this, OnNodeExited);
                _messenger.Register<BreakpointHitMessage>(this, OnBreakpointHit);
                _messenger.Register<GraphCompletedMessage>(this, OnGraphCompleted);
            }
        }

        private void OnGraphChanged(NodeGraph graph, GraphChange change)
        {
            switch (change)
            {
                case NodeAdded added:
                    {
                        var node = Model.GetNode(added.NodeId);
                        if (node != null && !_nodeMap.ContainsKey(node.Id))
                        {
                            AddNodeViewModel(node, added.Layout.X, added.Layout.Y);
                        }
                        // Undo: 노드 제거. Redo: 같은 위치/같은 인스턴스로 재추가.
                        PushUndoRedo(
                            undo: () => Model.RemoveNode(added.NodeId),
                            redo: () => { if (Model.GetNode(added.NodeId) == null && node != null) Model.AddNode(node, added.Layout.X, added.Layout.Y); });
                        break;
                    }
                case NodeRemoved removed:
                    {
                        var nodeSnap = _nodeMap.TryGetValue(removed.NodeId, out var nvmRemoved) ? nvmRemoved : null;
                        if (nvmRemoved != null)
                        {
                            Nodes.Remove(nvmRemoved);
                            _nodeMap.Remove(removed.NodeId);
                            if (SelectedNode == nvmRemoved) SelectedNode = null;
                            // 다중 선택에서도 제거 — 그래프 모델에서 빠진 노드는 어떤 선택 상태에도 남으면 안 됨.
                            if (SelectedNodes.Contains(nvmRemoved)) SelectedNodes.Remove(nvmRemoved);
                        }
                        if (nodeSnap != null)
                        {
                            // 노드 본체만 복원하는 단순 undo. 연결 복원은 Phase 7 강화 대상 (JSON 스냅샷).
                            var snapModel = nodeSnap.Model;
                            var snapX = nodeSnap.X;
                            var snapY = nodeSnap.Y;
                            PushUndoRedo(
                                undo: () => Model.AddNode(snapModel, snapX, snapY),
                                redo: () => Model.RemoveNode(removed.NodeId));
                        }
                        break;
                    }
                case Connected connected:
                    if (!_connectionMap.ContainsKey(connected.Connection.Id))
                    {
                        AddConnectionViewModel(connected.Connection);
                    }
                    PushUndoRedo(
                        undo: () => Model.Disconnect(connected.Connection.Id),
                        redo: () => { /* 같은 Id로 재생성 어려움 — 동일 source/target으로 새 Id 만들기. */
                            try
                            {
                                Model.Connect(connected.Connection.SourceNodeId, connected.Connection.SourcePinId,
                                              connected.Connection.TargetNodeId, connected.Connection.TargetPinId);
                            }
                            catch (InvalidOperationException) { }
                        });
                    break;
                case Disconnected disconnected:
                    Guid? removedSrc = null;
                    string removedSrcPin = null;
                    if (_connectionMap.TryGetValue(disconnected.ConnectionId, out var cvm))
                    {
                        removedSrc = cvm.Model.SourceNodeId;
                        removedSrcPin = cvm.Model.SourcePinId;
                        Connections.Remove(cvm);
                        _connectionMap.Remove(disconnected.ConnectionId);
                    }
                    if (!_suppressDisconnectedPushDuringNodeRemove && cvm != null)
                    {
                        var src = cvm.Model.SourceNodeId;
                        var srcPin = cvm.Model.SourcePinId;
                        var dst = cvm.Model.TargetNodeId;
                        var dstPin = cvm.Model.TargetPinId;
                        PushUndoRedo(
                            undo: () => { try { Model.Connect(src, srcPin, dst, dstPin); } catch (InvalidOperationException) { } },
                            redo: () => Model.Disconnect(disconnected.ConnectionId));
                    }
                    // 같은 소스 핀의 형제 인덱스를 재계산 (한 형제가 빠지면 나머지가 재정렬되어야 함)
                    if (removedSrc.HasValue) RecomputeSiblingIndices(removedSrc.Value, removedSrcPin);
                    break;
                case Moved moved:
                    if (_nodeMap.TryGetValue(moved.NodeId, out var mn))
                    {
                        mn.X = moved.To.X;
                        mn.Y = moved.To.Y;
                    }
                    PushUndoRedo(
                        undo: () => Model.MoveNode(moved.NodeId, moved.From.X, moved.From.Y),
                        redo: () => Model.MoveNode(moved.NodeId, moved.To.X, moved.To.Y));
                    break;
            }
        }

        private void PushUndoRedo(Action undo, Action redo)
        {
            if (_undoRedo == null || _suppressUndoRecording) return;
            _undoRedo.Push(
                undo: () => { _suppressUndoRecording = true; try { undo(); } finally { _suppressUndoRecording = false; } },
                redo: () => { _suppressUndoRecording = true; try { redo(); } finally { _suppressUndoRecording = false; } });
        }

        /// <summary>
        /// Phase K — 인스펙터의 NODE PROPERTIES 섹션이 변수 이름 후보를 가져올 소스.
        /// 호스트(GraphWorkspaceViewModel)가 그래프 변수 목록을 제공.
        /// </summary>
        public System.Func<System.Collections.Generic.IReadOnlyList<string>> VariableNameCandidatesProvider { get; set; }

        /// <summary>Phase K — ItemType 후보 목록 제공자. 호스트가 등록 가능한 CLR 타입 목록.</summary>
        public System.Func<System.Collections.Generic.IReadOnlyList<string>> TypeCandidatesProvider { get; set; }

        /// <summary>
        /// Phase M — 인스펙터의 Variable ComboBox 가 "+ Add new variable…" 항목을 노출하기 위한 콜백.
        /// 호스트(GraphWorkspaceViewModel)가 VariablesManagerWindow 를 띄워 신규 변수 등록 후 이름을 반환.
        /// </summary>
        public System.Func<string> AddNewVariableRequested { get; set; }

        private void AddNodeViewModel(INode node, double x, double y)
        {
            var nvm = new NodeViewModel(node, x, y)
            {
                VariableNameCandidatesProvider = VariableNameCandidatesProvider,
                TypeCandidatesProvider = TypeCandidatesProvider,
                AddNewVariableRequested = AddNewVariableRequested,
            };
            nvm.RefreshInstancePropertyCandidates();
            _nodeMap[node.Id] = nvm;
            Nodes.Add(nvm);
        }

        /// <summary>호스트가 변수/타입 목록 갱신 후 모든 노드의 인스펙터 후보 새로고침.</summary>
        public void RefreshAllInstancePropertyCandidates()
        {
            foreach (var n in Nodes) n.RefreshInstancePropertyCandidates();
        }

        private void AddConnectionViewModel(NodeConnection conn)
        {
            if (!_nodeMap.TryGetValue(conn.SourceNodeId, out var sourceVm)) return;
            if (!_nodeMap.TryGetValue(conn.TargetNodeId, out var targetVm)) return;
            var sourcePin = sourceVm.FindPin(conn.SourcePinId);
            var targetPin = targetVm.FindPin(conn.TargetPinId);
            if (sourcePin == null || targetPin == null) return;

            var cvm = new ConnectionViewModel(conn, sourceVm, sourcePin, targetVm, targetPin);
            _connectionMap[conn.Id] = cvm;
            Connections.Add(cvm);
            RecomputeSiblingIndices(conn.SourceNodeId, conn.SourcePinId);
        }

        /// <summary>
        /// 같은 소스 핀에서 나가는 연결들에 대해 SiblingIndex/SiblingCount를 재계산.
        /// N:M 시각화: 형제가 여럿일 때 베지어 곡률 오프셋이 자동 적용된다.
        /// </summary>
        private void RecomputeSiblingIndices(Guid sourceNodeId, string sourcePinId)
        {
            // 같은 (sourceNodeId, sourcePinId) 를 공유하는 ConnectionViewModel을 모은다.
            var siblings = new List<ConnectionViewModel>();
            foreach (var cvm in Connections)
            {
                if (cvm.Model.SourceNodeId == sourceNodeId && cvm.Model.SourcePinId == sourcePinId)
                {
                    siblings.Add(cvm);
                }
            }
            var count = siblings.Count;
            for (int i = 0; i < count; i++)
            {
                siblings[i].SiblingCount = count;
                siblings[i].SiblingIndex = i;
            }
        }

        public NodeViewModel FindNode(Guid id) =>
            _nodeMap.TryGetValue(id, out var v) ? v : null;

        /// <summary>
        /// SelectedNode setter — 단일 선택으로 리셋(다중 선택 클리어 후 1개만 선택).
        /// NodeView 의 modifier 없는 단순 클릭이 이 경로로 들어옴.
        /// 다중 선택 추가/토글은 AddToSelection/ToggleInSelection 사용 — OnSelectedNodeChanged 는
        /// SelectedNodes 가 이미 일관된 상태일 때만 호출되어야 하므로 setter 안에서 SelectedNodes 도 같이 갱신.
        /// </summary>
        partial void OnSelectedNodeChanged(NodeViewModel value)
        {
            // 다중 선택 컬렉션을 단일 선택 상태로 정렬.
            // (value == null 이면 SelectedNodes 도 비움. value != null 이면 그 1개만 남김.)
            if (!_suppressSelectedNodesSync)
            {
                _suppressSelectedNodesSync = true;
                try
                {
                    SelectedNodes.Clear();
                    if (value != null) SelectedNodes.Add(value);
                }
                finally { _suppressSelectedNodesSync = false; }
            }

            foreach (var n in Nodes)
            {
                n.IsSelected = SelectedNodes.Contains(n);
            }
        }

        // SelectedNodes 와 SelectedNode 가 서로를 트리거해 무한 재귀하지 않도록 가드.
        private bool _suppressSelectedNodesSync;

        /// <summary>
        /// Shift+클릭 — 기존 선택에 노드를 추가 (이미 있으면 무시).
        /// SelectedNode 는 마지막으로 추가된 노드를 가리키도록 갱신 (캔버스/툴바의 "현재 활성" 단일 노드).
        /// </summary>
        public void AddToSelection(NodeViewModel nvm)
        {
            if (nvm == null) return;
            if (!SelectedNodes.Contains(nvm))
            {
                SelectedNodes.Add(nvm);
                nvm.IsSelected = true;
            }
            _suppressSelectedNodesSync = true;
            try { SelectedNode = nvm; }
            finally { _suppressSelectedNodesSync = false; }
        }

        /// <summary>
        /// Ctrl+클릭 — 노드의 선택 상태를 토글. 마지막 토글 결과로 SelectedNode 갱신.
        /// </summary>
        public void ToggleInSelection(NodeViewModel nvm)
        {
            if (nvm == null) return;
            if (SelectedNodes.Contains(nvm))
            {
                SelectedNodes.Remove(nvm);
                nvm.IsSelected = false;
                _suppressSelectedNodesSync = true;
                try { SelectedNode = SelectedNodes.LastOrDefault(); }
                finally { _suppressSelectedNodesSync = false; }
            }
            else
            {
                AddToSelection(nvm);
            }
        }

        /// <summary>박스 선택 등 외부 진입점 — 한 번에 여러 노드를 추가 선택.</summary>
        public void AddRangeToSelection(IEnumerable<NodeViewModel> nodes)
        {
            if (nodes == null) return;
            NodeViewModel last = null;
            foreach (var n in nodes)
            {
                if (n == null || SelectedNodes.Contains(n)) continue;
                SelectedNodes.Add(n);
                n.IsSelected = true;
                last = n;
            }
            if (last != null)
            {
                _suppressSelectedNodesSync = true;
                try { SelectedNode = last; }
                finally { _suppressSelectedNodesSync = false; }
            }
        }

        /// <summary>모든 선택 해제 (캔버스 빈 영역 클릭 시).</summary>
        public void ClearSelection()
        {
            foreach (var n in SelectedNodes) n.IsSelected = false;
            SelectedNodes.Clear();
            _suppressSelectedNodesSync = true;
            try { SelectedNode = null; }
            finally { _suppressSelectedNodesSync = false; }
        }

        /// <summary>Ctrl+A — 모든 노드 선택.</summary>
        [RelayCommand]
        private void SelectAll()
        {
            if (Nodes.Count == 0) return;
            _suppressSelectedNodesSync = true;
            try
            {
                SelectedNodes.Clear();
                foreach (var n in Nodes)
                {
                    n.IsSelected = true;
                    SelectedNodes.Add(n);
                }
                SelectedNode = Nodes.LastOrDefault();
            }
            finally { _suppressSelectedNodesSync = false; }
        }

        // === Commands ===

        /// <summary>새 노드를 그래프에 추가한다. typeId는 NodeMetadataRegistry에 등록되어 있어야 한다.</summary>
        [RelayCommand]
        private void AddNode((string typeId, double x, double y) args)
        {
            Model.AddNode(args.typeId, args.x, args.y);
        }

        /// <summary>현재 선택된 노드(들)을 삭제한다. 다중 선택이면 모두 삭제.
        /// 노드 제거 도중 발생하는 부산물 Disconnected 이벤트는 별도 push하지 않고
        /// NodeRemoved push N개로 묶는다 (Undo 시 노드만 복원, 연결 복원은 추후 Phase 강화 대상).</summary>
        [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
        private void RemoveSelected()
        {
            // SelectedNodes 가 우선 — 다중 삭제. 비어있으면 SelectedNode 한 개 시도.
            var toRemove = SelectedNodes.Count > 0
                ? SelectedNodes.Select(n => n.Id).ToList()
                : (SelectedNode != null ? new List<Guid> { SelectedNode.Id } : new List<Guid>());
            if (toRemove.Count == 0) return;

            _suppressDisconnectedPushDuringNodeRemove = true;
            try
            {
                foreach (var id in toRemove) Model.RemoveNode(id);
            }
            finally
            {
                _suppressDisconnectedPushDuringNodeRemove = false;
            }
        }

        private bool CanRemoveSelected() => SelectedNodes.Count > 0 || SelectedNode != null;

        // ================= 클립보드 / 자동 정렬 =================

        /// <summary>
        /// 앱 내부 클립보드(프로세스 전역, 모든 NodeGraphViewModel 인스턴스 간 공유).
        /// JSON DTO 형식 — 노드 N개 + 그 노드들 사이의 연결만 포함. 외부 연결(선택 안 된 노드로 가는 연결) 은 제외.
        /// (시스템 클립보드 사용 안 함 — 의도적으로 앱 내부 한정.)
        /// </summary>
        private static ClipboardPayload _clipboard;

        private sealed class ClipboardPayload
        {
            public List<NodeSnapshot> Nodes { get; } = new();
            public List<ConnSnapshot> Connections { get; } = new();
            /// <summary>복사 당시 노드들의 좌상단 origin — 붙여넣기 시 마우스/오프셋 기준으로 재배치.</summary>
            public double OriginX { get; set; }
            public double OriginY { get; set; }
        }

        private sealed class NodeSnapshot
        {
            public Guid OriginalId { get; set; }
            public string TypeId { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public Dictionary<string, JsonElement> LiteralInputs { get; } = new();
            public JsonElement? State { get; set; }
        }

        private sealed class ConnSnapshot
        {
            public Guid SourceOriginalId { get; set; }
            public string SourcePinId { get; set; }
            public Guid TargetOriginalId { get; set; }
            public string TargetPinId { get; set; }
        }

        /// <summary>
        /// Ctrl+C — 현재 선택된 노드(들) 을 앱 내부 클립보드에 복사.
        /// 직렬화는 LiteralInputs + WriteState 후크만 — Guid 는 붙여넣기 시 새로 할당.
        /// 선택 노드들 사이의 연결만 포함 (외부로 가는 연결은 무시).
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCopy))]
        private void Copy()
        {
            var selected = SelectedNodes.Count > 0
                ? SelectedNodes.ToList()
                : (SelectedNode != null ? new List<NodeViewModel> { SelectedNode } : new List<NodeViewModel>());
            if (selected.Count == 0) return;

            var payload = new ClipboardPayload();
            double minX = double.MaxValue, minY = double.MaxValue;
            var selectedIds = new HashSet<Guid>(selected.Select(n => n.Id));

            foreach (var nvm in selected)
            {
                if (nvm.X < minX) minX = nvm.X;
                if (nvm.Y < minY) minY = nvm.Y;

                var snap = new NodeSnapshot
                {
                    OriginalId = nvm.Id,
                    TypeId = nvm.TypeId,
                    X = nvm.X,
                    Y = nvm.Y,
                };

                if (nvm.Model is NodeBase nb)
                {
                    foreach (var kv in nb.LiteralInputs)
                    {
                        try { snap.LiteralInputs[kv.Key] = JsonSerializer.SerializeToElement(kv.Value); }
                        catch { /* 직렬화 불가 값은 스킵 */ }
                    }
                    // WriteState 후크 — 빈 객체가 아니면만 저장.
                    try
                    {
                        using var ms = new System.IO.MemoryStream();
                        using (var writer = new System.Text.Json.Utf8JsonWriter(ms))
                        {
                            writer.WriteStartObject();
                            nb.WriteState(writer);
                            writer.WriteEndObject();
                        }
                        using var stateDoc = JsonDocument.Parse(ms.ToArray());
                        if (stateDoc.RootElement.EnumerateObject().MoveNext())
                        {
                            snap.State = stateDoc.RootElement.Clone();
                        }
                    }
                    catch { /* state 직렬화 실패 시 스킵 */ }
                }
                payload.Nodes.Add(snap);
            }

            payload.OriginX = minX;
            payload.OriginY = minY;

            // 양 끝이 모두 선택된 연결만 보존.
            foreach (var c in Model.Connections)
            {
                if (selectedIds.Contains(c.SourceNodeId) && selectedIds.Contains(c.TargetNodeId))
                {
                    payload.Connections.Add(new ConnSnapshot
                    {
                        SourceOriginalId = c.SourceNodeId,
                        SourcePinId = c.SourcePinId,
                        TargetOriginalId = c.TargetNodeId,
                        TargetPinId = c.TargetPinId,
                    });
                }
            }

            _clipboard = payload;
        }

        private bool CanCopy() => SelectedNodes.Count > 0 || SelectedNode != null;

        /// <summary>
        /// Ctrl+V — 클립보드의 노드(들) 을 그래프에 붙여넣음.
        /// 좌표는 클립보드 origin 대비 (+40,+40) 오프셋 (반복 붙여넣기마다 캐스케이드).
        /// 새 노드는 NodeBase 가 자동 발급한 새 Guid 를 가지며, OriginalId 매핑으로 내부 연결도 복원.
        /// 붙여넣기 직후 새로 만들어진 노드들이 다중 선택 상태가 됨.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPaste))]
        private void Paste()
        {
            var payload = _clipboard;
            if (payload == null || payload.Nodes.Count == 0) return;

            const double pasteOffset = 40.0;
            // 매 호출마다 오프셋이 누적되도록 클립보드 origin 자체를 갱신 — 두 번 누르면 (80,80).
            payload.OriginX -= pasteOffset;
            payload.OriginY -= pasteOffset;

            var idMap = new Dictionary<Guid, Guid>(payload.Nodes.Count);
            var createdVms = new List<NodeViewModel>(payload.Nodes.Count);

            foreach (var snap in payload.Nodes)
            {
                INode node;
                try
                {
                    var newX = snap.X - payload.OriginX; // == (snap.X - origOriginX) + pasteOffset
                    var newY = snap.Y - payload.OriginY;
                    node = Model.AddNode(snap.TypeId, newX, newY);
                }
                catch (InvalidOperationException)
                {
                    continue; // 알 수 없는 typeId — 다른 그래프에서 복사 후 해당 어셈블리 미등록인 경우 스킵.
                }

                idMap[snap.OriginalId] = node.Id;

                if (node is NodeBase nb)
                {
                    var meta = NodeMetadataRegistry.Get(snap.TypeId);
                    foreach (var kv in snap.LiteralInputs)
                    {
                        var pinDesc = meta?.Pins.FirstOrDefault(p => p.Id == kv.Key);
                        if (pinDesc == null) continue;
                        if (pinDesc.Kind != PinKind.Data || pinDesc.Direction != PinDirection.Input) continue;
                        try
                        {
                            var value = kv.Value.Deserialize(pinDesc.ValueType);
                            if (value != null) nb.SetLiteralInput(kv.Key, value);
                        }
                        catch (JsonException) { /* 타입 불일치 — 스킵 */ }
                    }
                    if (snap.State.HasValue && snap.State.Value.ValueKind == JsonValueKind.Object)
                    {
                        try { nb.ReadState(snap.State.Value); } catch { /* state 복원 실패 — 무시 */ }
                    }
                }

                var nvm = FindNode(node.Id);
                if (nvm != null) createdVms.Add(nvm);
            }

            // 내부 연결 복원 (선택 노드들 사이의 것만 복사했으므로 양 끝 모두 매핑되어야 정상).
            foreach (var c in payload.Connections)
            {
                if (!idMap.TryGetValue(c.SourceOriginalId, out var srcId)) continue;
                if (!idMap.TryGetValue(c.TargetOriginalId, out var dstId)) continue;
                try { Model.Connect(srcId, c.SourcePinId, dstId, c.TargetPinId); }
                catch (InvalidOperationException) { /* 호환성 위반 — 스킵 */ }
            }

            // 새로 만든 노드들로 선택 상태 교체 — 사용자가 곧바로 다시 이동/복사 가능.
            ClearSelection();
            AddRangeToSelection(createdVms);
        }

        private bool CanPaste() => _clipboard != null && _clipboard.Nodes.Count > 0;

        /// <summary>
        /// 그래프 자동 정렬/연결선 방향. 가로(좌→우 흐름) vs 세로(위→아래 흐름).
        /// AutoLayout 결과 + ConnectionView 베지어 컨트롤 포인트 방향이 이 값을 따른다.
        /// </summary>
        [Property] private GraphLayoutOrientation _layoutOrientation = GraphLayoutOrientation.Horizontal;

        /// <summary>가로 정렬 (Start 좌측 → End 우측 컬럼). Ctrl+L 단축키가 이걸 호출.</summary>
        [RelayCommand]
        private void AutoLayoutHorizontal()
        {
            LayoutOrientation = GraphLayoutOrientation.Horizontal;
            ApplyTopologicalLayout(GraphLayoutOrientation.Horizontal);
        }

        /// <summary>세로 정렬 (Start 상단 → End 하단 행). Netron 스타일.</summary>
        [RelayCommand]
        private void AutoLayoutVertical()
        {
            LayoutOrientation = GraphLayoutOrientation.Vertical;
            ApplyTopologicalLayout(GraphLayoutOrientation.Vertical);
        }

        /// <summary>
        /// 하위 호환 — 기존 단축키(Ctrl+L) 가 호출하는 명령. 현재 LayoutOrientation 기준으로 재정렬.
        /// 처음 호출 시 기본 Horizontal 이며 사용자가 명시적으로 Vertical 명령을 호출한 뒤에는 그 방향을 유지.
        /// </summary>
        [RelayCommand]
        private void AutoLayout()
        {
            ApplyTopologicalLayout(LayoutOrientation);
        }

        /// <summary>
        /// Topological-columns 자동 정렬 구현.
        /// 알고리즘: exec(In→Then/True/False) 연결만 기준으로 longest-path-from-source 를 계산해 각 노드의 "depth" 결정.
        /// 가로 모드면 depth=X 컬럼, 세로 모드면 depth=Y 행. 같은 depth 내 노드는 원래 좌표 순서를 보존하며 분산.
        /// 사이클이 있어도 SCC 단위로 같은 depth 처리.
        /// </summary>
        private void ApplyTopologicalLayout(GraphLayoutOrientation orientation)
        {
            if (Nodes.Count == 0) return;

            // 가로/세로 모드에 따라 축 의미가 바뀜:
            //   Horizontal: depth → X 좌표, 같은 depth 내 순번 → Y 좌표
            //   Vertical:   depth → Y 좌표, 같은 depth 내 순번 → X 좌표
            // 노드 시각 크기가 가로(~200px) × 세로(~80px) 로 비대칭이라 각 모드의 자연 간격이 다르다.
            // - Horizontal: depth 가 X 이므로 노드 폭(200) + 여유; crossStep 은 노드 높이(80) + 여유.
            // - Vertical:   depth 가 Y 이므로 노드 높이(80) + 여유; crossStep 은 노드 폭(200) + 여유.
            double depthStep, crossStep;
            if (orientation == GraphLayoutOrientation.Horizontal)
            {
                depthStep = 280.0;
                crossStep = 140.0;
            }
            else // Vertical
            {
                depthStep = 160.0;   // 위→아래 — 노드 높이 80 + 80 여유 (베지어 곡률 공간 포함)
                crossStep = 280.0;   // 좌→우 — 노드 폭 200 + 80 여유
            }
            const double origin = 60.0;

            // 1) exec 인접 리스트 구축.
            var execOut = new Dictionary<Guid, List<Guid>>();
            var execIn = new Dictionary<Guid, List<Guid>>();
            foreach (var n in Nodes)
            {
                execOut[n.Id] = new List<Guid>();
                execIn[n.Id] = new List<Guid>();
            }
            foreach (var c in Model.Connections)
            {
                if (c.Kind != PinKind.Exec) continue;
                if (!execOut.ContainsKey(c.SourceNodeId) || !execIn.ContainsKey(c.TargetNodeId)) continue;
                execOut[c.SourceNodeId].Add(c.TargetNodeId);
                execIn[c.TargetNodeId].Add(c.SourceNodeId);
            }

            // 2) Longest-path depth 계산.
            var depth = new Dictionary<Guid, int>(Nodes.Count);
            foreach (var n in Nodes) depth[n.Id] = 0;

            var indeg = new Dictionary<Guid, int>(Nodes.Count);
            foreach (var n in Nodes) indeg[n.Id] = execIn[n.Id].Count;

            var queue = new Queue<Guid>();
            foreach (var n in Nodes) if (indeg[n.Id] == 0) queue.Enqueue(n.Id);

            while (queue.Count > 0)
            {
                var u = queue.Dequeue();
                foreach (var v in execOut[u])
                {
                    if (depth[v] < depth[u] + 1) depth[v] = depth[u] + 1;
                    if (--indeg[v] == 0) queue.Enqueue(v);
                }
            }

            // 3) depth 별 그룹화. 같은 depth 내 정렬 기준: 가로 모드는 Y 우선(좌→우 흐름 아래에서 위/아래 분포),
            //    세로 모드는 X 우선(위→아래 흐름 아래에서 좌/우 분포).
            var byDepth = Nodes
                .GroupBy(n => depth[n.Id])
                .OrderBy(g => g.Key)
                .ToList();

            // 4) 위치 적용.
            foreach (var group in byDepth)
            {
                var ordered = orientation == GraphLayoutOrientation.Horizontal
                    ? group.OrderBy(n => n.Y).ThenBy(n => n.X).ToList()
                    : group.OrderBy(n => n.X).ThenBy(n => n.Y).ToList();

                for (int i = 0; i < ordered.Count; i++)
                {
                    var nvm = ordered[i];
                    double nx, ny;
                    if (orientation == GraphLayoutOrientation.Horizontal)
                    {
                        nx = origin + group.Key * depthStep;
                        ny = origin + i * crossStep;
                    }
                    else
                    {
                        nx = origin + i * crossStep;
                        ny = origin + group.Key * depthStep;
                    }
                    if (Math.Abs(nvm.X - nx) > 0.5 || Math.Abs(nvm.Y - ny) > 0.5)
                    {
                        Model.MoveNode(nvm.Id, nx, ny);
                    }
                }
            }
        }

        /// <summary>
        /// 다중 선택된 노드를 통째로 (dx, dy) 만큼 이동. NodeView 의 드래그가 호출.
        /// 드래그 종료 시점에 호출되는 게 아니라 매 MouseMove 마다 호출 — Model.MoveNode 가 아닌 VM X/Y 만 즉시 갱신.
        /// (드래그 종료 후 별도로 MoveNodeCommand 가 Model.MoveNode 로 commit — 그 경로는 NodeView 가 담당.)
        /// </summary>
        public void TranslateSelectionBy(double dx, double dy, NodeViewModel except = null)
        {
            if (SelectedNodes.Count <= 1) return; // 단일 노드는 NodeView 자체 드래그로 충분.
            foreach (var n in SelectedNodes)
            {
                if (n == except) continue;
                n.X += dx;
                n.Y += dy;
            }
        }

        /// <summary>드래그 종료 시 — 다중 선택의 새 좌표를 Model.MoveNode 로 commit (undo 가능).</summary>
        public void CommitSelectionMove()
        {
            foreach (var n in SelectedNodes)
            {
                Model.MoveNode(n.Id, n.X, n.Y);
            }
        }

        /// <summary>두 핀을 연결한다. 호환성/카디널리티 위반 시 NodeGraph.Connect가 throw.</summary>
        [RelayCommand]
        private void Connect((PinViewModel source, PinViewModel target) args)
        {
            if (args.source == null || args.target == null) return;
            try
            {
                Model.Connect(args.source.Node.Id, args.source.Id, args.target.Node.Id, args.target.Id);
            }
            catch (InvalidOperationException)
            {
                // UI 측에선 silent fail — 피드백은 Phase 6 이상에서 토스트 등으로.
            }
        }

        /// <summary>연결을 삭제한다.</summary>
        [RelayCommand]
        private void Disconnect(ConnectionViewModel connection)
        {
            if (connection == null) return;
            Model.Disconnect(connection.Id);
        }

        /// <summary>노드 위치를 이동한다 (드래그 종료 시 1회 호출).</summary>
        [RelayCommand]
        private void MoveNode((Guid nodeId, double x, double y) args)
        {
            Model.MoveNode(args.nodeId, args.x, args.y);
        }

        /// <summary>NodeMetadataRegistry.All의 한 항목을 클릭 좌표에 노드로 추가하는 편의 메소드 (팔레트용).</summary>
        public NodeViewModel AddNodeFromMetadata(NodeMetadata metadata, double x, double y)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            var node = Model.AddNode(metadata.TypeId, x, y);
            return FindNode(node.Id);
        }

        // === Scheduler commands (Phase 7) ===

        private bool CanRun() => _scheduler != null && !IsRunning && SelectedNode != null;
        private bool CanStop() => _scheduler != null && IsRunning;
        private bool CanContinueOrStep() => _scheduler != null && IsPaused;

        /// <summary>선택된 노드를 진입점으로 그래프를 실행한다. ISchedulerService가 주입되어야 함.</summary>
        [AsyncRelayCommand(CanExecute = nameof(CanRun))]
        private async Task Run()
        {
            if (_scheduler == null || SelectedNode == null) return;
            BuiltInNodes.EnsureRegistered();

            _runCts = new CancellationTokenSource();
            IsRunning = true;
            IsPaused = false;
            // 새 Run — 이전 Run 의 시각적 상태(초록/빨강 보더) 모두 원복.
            foreach (var n in Nodes)
            {
                n.IsExecuting = false;
                n.HasExecutedInCurrentRun = false;
                n.HasErrorInCurrentRun = false;
            }
            var ctx = new ExecutionContext(Model, _runCts.Token, logger: null, messenger: _messenger);
            ctx.HistoryStore = HistoryStore;
            ctx.LogSink = LogSink;

            // ViewModel의 브레이크포인트(HasBreakpoint)를 컨텍스트에 복사
            foreach (var n in Nodes)
            {
                if (n.HasBreakpoint) ctx.Breakpoints.Add(n.Id);
            }

            // 호스트 후크 — ctx.Variables 등에 임의 보관소 주입 가능. Run 시작 직전 호출.
            ConfigureContext?.Invoke(ctx);

            try
            {
                // 사용자 코드 노드(CustomFunctionNode 등) 가 동기 CPU 작업(예: 1억회 픽셀 루프) 을 수행하면
                // 호출 스레드(UI)를 끝까지 점유해 앱이 멈춘다. Task.Run 으로 ThreadPool 에서 실행 →
                // 메시지 핸들러는 capture 한 SynchronizationContext 로 UI 마샬링 되어 안전하게 갱신.
                // ConfigureAwait(true) 로 await 후 다시 UI 스레드로 돌아와 IsRunning/IsPaused setter 등이 안전.
                var entryId = SelectedNode.Id;
                await Task.Run(() => _scheduler.RunAsync(Model, entryId, ctx), ctx.CancellationToken)
                    .ConfigureAwait(true);
            }
            finally
            {
                _runCts?.Dispose();
                _runCts = null;
            }
        }

        /// <summary>실행 중인 그래프를 취소한다.</summary>
        [RelayCommand(CanExecute = nameof(CanStop))]
        private void Stop()
        {
            _runCts?.Cancel();
        }

        /// <summary>일시정지된 실행을 재개한다.</summary>
        [RelayCommand(CanExecute = nameof(CanContinueOrStep))]
        private void Continue()
        {
            _scheduler?.Continue();
            IsPaused = false;
        }

        /// <summary>일시정지 상태에서 한 노드만 진행하고 다시 정지한다.</summary>
        [RelayCommand(CanExecute = nameof(CanContinueOrStep))]
        private void StepOver()
        {
            _scheduler?.StepOver();
            IsPaused = false;
        }

        /// <summary>지정 노드의 브레이크포인트를 토글. ViewModel 상태를 소스 오브 트루스로 삼아
        /// scheduler 의 글로벌 등록을 명시적으로 동기화 (XOR toggle 이 아니라서 상태 불일치 발생 안 함).</summary>
        [RelayCommand]
        private void ToggleBreakpoint(NodeViewModel target)
        {
            target ??= SelectedNode;
            if (target == null) return;
            target.HasBreakpoint = !target.HasBreakpoint;
            _scheduler?.SetBreakpoint(target.Id, target.HasBreakpoint);
        }

        // === Messenger handlers (Phase 7) ===
        // 모든 핸들러는 SchedulerService 의 백그라운드 스레드에서 호출될 수 있다 (Messenger.Send 가 동기 invoke).
        // ObservableCollection (LastInputs/LastOutputs) 변경 + 바인딩된 속성 setter 를 안전하게 처리하려면
        // 생성 시 capture 한 SynchronizationContext (보통 UI) 로 마샬링해야 한다.

        /// <summary>
        /// UI 스레드로 마샬링. capture 한 SynchronizationContext 우선, 없으면 Application.Current.Dispatcher 로 폴백.
        /// _capturedContext 가 null 인 경우 (뷰모델이 UI Dispatcher.Run 이전에 생성된 경우 등) 에도 안전.
        /// </summary>
        private void MarshalToUI(Action action)
        {
            if (action == null) return;
            var ctx = _capturedContext;
            if (ctx != null && ctx != System.Threading.SynchronizationContext.Current)
            {
                ctx.Post(_ => action(), null);
                return;
            }
            // capture 실패 or 이미 같은 컨텍스트 — Dispatcher 폴백으로 UI 스레드 여부 재확인.
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(action);
                return;
            }
            action();
        }

        private void OnNodeEntering(object sender, NodeEnteringMessage msg) => MarshalToUI(() =>
        {
            var nvm = FindNode(msg.NodeId);
            if (nvm != null)
            {
                // 정지 풀려 실제 실행 시작 — paused 표시 해제.
                nvm.IsCurrentBreakpoint = false;
                nvm.IsExecuting = true;
            }
        });

        private void OnNodeExited(object sender, NodeExitedMessage msg) => MarshalToUI(() =>
        {
            var nvm = FindNode(msg.NodeId);
            if (nvm != null)
            {
                nvm.IsExecuting = false;
                // 지나간 흐름을 시각적으로 남기기 위해 — Run 이 진행되는 동안 보더 유지.
                // Success=true 면 초록, false 면 빨강.
                if (msg.Success)
                {
                    nvm.HasExecutedInCurrentRun = true;
                }
                else
                {
                    nvm.HasErrorInCurrentRun = true;
                }
                nvm.LastElapsed = msg.Elapsed;
                nvm.UpdateSnapshots(msg.Inputs, msg.Outputs);
            }
            Profiling.Record(msg.NodeId, msg.Elapsed);
        });

        private void OnBreakpointHit(object sender, BreakpointHitMessage msg) => MarshalToUI(() =>
        {
            IsPaused = true;
            // 정지 위치 시각화 — 한 노드만 IsCurrentBreakpoint=true 로 유지.
            // 알 수 없는 NodeId 라도 "현재 paused 위치 갱신" 의미로 기존 표시는 모두 clear.
            foreach (var n in Nodes)
            {
                n.IsCurrentBreakpoint = (n.Id == msg.NodeId);
            }
        });

        private void OnGraphCompleted(object sender, GraphCompletedMessage msg) => MarshalToUI(() =>
        {
            IsRunning = false;
            IsPaused = false;
            // Run 종료 — 성공 흔적 + paused 표시 모두 원복.
            // 에러 흔적은 사용자가 어떤 노드에서 실패했는지 확인할 수 있도록 유지 — 다음 Run 시작 시에만 리셋.
            foreach (var n in Nodes)
            {
                n.IsExecuting = false;
                n.HasExecutedInCurrentRun = false;
                n.IsCurrentBreakpoint = false;
            }
        });
    }
}
