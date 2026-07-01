using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Compilation;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.Core.Scheduler.Serialization;
using VSMVVM.WPF.Scheduler.Services;
using VSMVVM.WPF.Services;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// 멀티 탭 워크스페이스 1개. 자기만의 NodeGraph + GraphVm + Variables + CodeSnippets +
    /// 로그/히스토리 보관소를 보유. 호스트가 N 개의 워크스페이스를 동시에 관리하여 멀티 탭 UI 를 구성한다.
    /// <para>
    /// 사용자 결정 정책:
    ///   • 새 워크스페이스는 빈 그래프 (노드 0개).
    ///   • CodeSnippets 컬렉션이 카테고리별 사용자 코드를 보관 (Save/Load JSON 시 Extras["userCode.&lt;category&gt;"] dict 로 라운드트립).
    ///   • <see cref="CompileAllSnippetsCommand"/> 가 모든 조각을 컴파일·등록 — 각 조각의 RegisteredTypeIds 가
    ///     재컴파일 시 자동 정리되어 typeId 충돌은 "마지막 컴파일이 이긴다".
    ///   • 카테고리는 <see cref="UserCodeCategoryExtractor"/> 가 컴파일된 어셈블리에서 자동 추출.
    /// </para>
    /// <para>
    /// Services 는 모든 워크스페이스가 공유 (compiler/scheduler/messenger 등은 호스트가 주입).
    /// 데모-특화 그래프 빌더(LoadOpenCvDemo 등) 는 호스트의 sample subclass 가 <see cref="RebuildGraph"/> 로 처리.
    /// </para>
    /// </summary>
    public partial class GraphWorkspaceViewModel : ViewModelBase
    {
        protected readonly ICompilationService _compiler;
        protected readonly ISchedulerService _scheduler;
        protected readonly IMessenger _messenger;
        protected readonly IUndoRedoService _undo;
        protected readonly INodePaletteService _palette;
        protected readonly ILoggerService _logger;
        protected readonly IFileDialogService _fileDialog;
        protected readonly IWindowService _windowService;

        /// <summary>워크스페이스의 모델 그래프. 사용자 결정: 새 워크스페이스는 빈 그래프.</summary>
        public NodeGraph Model { get; private set; }

        /// <summary>탭 헤더에 표시되는 이름. 사용자가 편집 가능.</summary>
        [Property] private string _displayName;

        /// <summary>그래프 ViewModel. Run/Stop/Continue/Step/Breakpoint 명령은 여기에 있음.</summary>
        [Property] private NodeGraphViewModel _graphVm;

        /// <summary>현재 보여줄 팔레트 카테고리 목록. 컴파일 후 자동 갱신.
        /// 검색어가 있으면 필터링된 결과만 노출.</summary>
        [Property] private IReadOnlyList<NodePaletteCategory> _paletteCategories = Array.Empty<NodePaletteCategory>();

        /// <summary>팔레트 검색어 — 변경 시 자동으로 PaletteCategories 가 필터링됨.
        /// 빈/null 이면 전체 카테고리 노출.</summary>
        [Property] private string _paletteSearchQuery = string.Empty;

        /// <summary>팔레트 카테고리 목록을 현재 NodeMetadataRegistry 상태 + 현재 검색어로 갱신.
        /// 사용자 코드 에디터가 단일 조각만 컴파일한 후 호출, 또는 검색어 변경 시 자동 호출.</summary>
        public void RefreshPaletteCategories()
        {
            PaletteCategories = string.IsNullOrWhiteSpace(PaletteSearchQuery)
                ? _palette.GetCategories()
                : _palette.Search(PaletteSearchQuery);
        }

        partial void OnPaletteSearchQueryChanged(string value)
        {
            RefreshPaletteCategories();
        }

        /// <summary>최근 동작 결과/에러 메시지. UI 하단 status 표시용.</summary>
        [Property] private string _statusMessage = string.Empty;

        /// <summary>워크스페이스 단위 실행 히스토리 보관소.</summary>
        public IExecutionHistoryStore HistoryStore { get; } = new InMemoryExecutionHistoryStore(capacity: 50);

        /// <summary>워크스페이스 단위 로그 sink.</summary>
        public ISchedulerLogSink LogSink { get; } = new InMemorySchedulerLogSink(capacity: 500);

        /// <summary>로그 패널 ViewModel.</summary>
        public SchedulerLogViewModel LogVm { get; }

        /// <summary>실행 히스토리 패널 ViewModel.</summary>
        public ExecutionHistoryViewModel HistoryVm { get; }

        /// <summary>NodeGraph.Variables 의 거울 — UI 사이드바가 바인딩.</summary>
        public ObservableCollection<GraphVariable> Variables { get; } = new();

        // === Variables 사이드바 입력 필드 ===
        [Property] private string _newVariableName = "myVar";
        [Property] private string _newVariableTypeName = "int";
        [Property] private string _newVariableDefault = "0";

        /// <summary>드롭다운 옵션 — 사이드바에서 선택 가능한 타입 라벨. 호스트가 늘릴 수 있도록 virtual.</summary>
        public virtual IReadOnlyList<string> VariableTypeOptions { get; } = new[] { "int", "double", "string", "bool", "long" };

        /// <summary>
        /// Run 시작 직전 ExecutionContext 에 호스트가 추가 정보를 주입할 수 있는 후크.
        /// 예: ImageSnapshotStore 같은 sample-특화 보관소를 ctx.Variables 에 push.
        /// </summary>
        public Action<ExecutionContext> ConfigureContext { get; set; }

        public GraphWorkspaceViewModel(
            ICompilationService compiler,
            INodePaletteService palette,
            ISchedulerService scheduler = null,
            IMessenger messenger = null,
            IUndoRedoService undo = null,
            ILoggerService logger = null,
            IFileDialogService fileDialog = null,
            IWindowService windowService = null,
            string displayName = "Workspace")
        {
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            _palette = palette ?? throw new ArgumentNullException(nameof(palette));
            _scheduler = scheduler;
            _messenger = messenger;
            _undo = undo;
            _logger = logger;
            _fileDialog = fileDialog;
            _windowService = windowService;

            _displayName = displayName;
            Model = new NodeGraph { Name = displayName };
            _graphVm = CreateGraphVm(Model);
            _paletteCategories = _palette.GetCategories();

            LogVm = new SchedulerLogViewModel(LogSink);
            HistoryVm = new ExecutionHistoryViewModel(HistoryStore) { Graph = _graphVm };
        }

        /// <summary>
        /// NodeGraphViewModel 인스턴스를 만들면서 HistoryStore / LogSink / ConfigureContext 후크를 빠짐없이 설정.
        /// ClearAndRebuildGraph / LoadJson 에서 새 GraphVm 을 만들 때마다 호출되어야 회귀 방지.
        /// </summary>
        private NodeGraphViewModel CreateGraphVm(NodeGraph graph)
        {
            var vm = new NodeGraphViewModel(graph, _undo, _scheduler, _messenger)
            {
                HistoryStore = HistoryStore,
                LogSink = LogSink,
                ConfigureContext = ctx => ConfigureContext?.Invoke(ctx),
                // Phase K — 인스펙터의 NODE PROPERTIES 가 변수/타입 후보를 가져올 소스.
                VariableNameCandidatesProvider = () => GetVariableNameCandidates(),
                TypeCandidatesProvider = () => GetTypeCandidates(),
                // Phase M — 인스펙터 ComboBox 의 "+ Add new variable…" 선택 시 호출.
                AddNewVariableRequested = () => OnAddNewVariableRequested(),
            };
            return vm;
        }

        /// <summary>
        /// Phase M — 인스펙터의 Variable ComboBox 에서 "+ Add new variable…" 선택 시 호출.
        /// 기본 구현: 자동으로 "var{N}" + typeof(object) + null default 의 변수 추가 후 이름 반환.
        /// Sample 이 override 하여 VariablesManagerWindow 모달로 사용자 입력 받을 수 있음.
        /// 반환 null/empty 면 사용자 취소 — 인스펙터는 Value 유지.
        /// </summary>
        protected virtual string OnAddNewVariableRequested()
        {
            int n = 1;
            string name;
            do { name = $"var{n++}"; } while (Model.Variables.ContainsKey(name));
            AddVariable(name, typeof(object), null);
            return name;
        }

        /// <summary>그래프에 정의된 변수 이름 목록 — 인스펙터 ComboBox 의 후보값.</summary>
        protected virtual IReadOnlyList<string> GetVariableNameCandidates()
        {
            if (Model?.Variables == null) return Array.Empty<string>();
            var list = new List<string>(Model.Variables.Count);
            foreach (var v in Model.Variables.Values) list.Add(v.Name);
            return list;
        }

        /// <summary>인스펙터 ItemType ComboBox 의 후보 stable name 목록. 기본은 ResolveTypeFromLabel 이 아는 라벨들 + 그래프에 정의된 변수들의 타입.</summary>
        protected virtual IReadOnlyList<string> GetTypeCandidates()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            // 기본 primitive
            foreach (var t in new[] { typeof(int), typeof(double), typeof(string), typeof(bool), typeof(long), typeof(object) })
                set.Add(Core.Scheduler.Pins.PinTypeInfo.ComputeStableName(t));
            // 그래프에 정의된 변수들의 CLR 타입도 포함 (Mat 같은 사용자 타입 자동 노출)
            if (Model?.Variables != null)
            {
                foreach (var v in Model.Variables.Values)
                    if (v.ClrType != null) set.Add(Core.Scheduler.Pins.PinTypeInfo.ComputeStableName(v.ClrType));
            }
            return set.ToList();
        }

        // ============= 그래프 재구축 =============

        /// <summary>
        /// 워크스페이스의 그래프를 비우고 builder 로 새 노드/연결을 구축. GraphVm 을 새로 만들어 캔버스가 자동 재바인딩.
        /// sample 의 데모 명령(LoadOpenCvDemo 등) 이 이걸 호출.
        /// </summary>
        public void RebuildGraph(Action<NodeGraph> builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var nodeIds = Model.Nodes.Select(n => n.Id).ToList();
            foreach (var id in nodeIds) Model.RemoveNode(id);
            var varNames = Model.Variables.Keys.ToList();
            foreach (var n in varNames) Model.RemoveVariable(n);

            builder(Model);

            GraphVm = CreateGraphVm(Model);
            GraphVm.SelectedNode = GraphVm.Nodes.FirstOrDefault(n => n.TypeId == StartNode.TypeIdConst);
            SyncVariablesFromGraph();
            PaletteCategories = _palette.GetCategories();
        }

        // ============= Variables 사이드바 =============

        /// <summary>NodeGraph.Variables 와 ObservableCollection 동기화.</summary>
        public void SyncVariablesFromGraph()
        {
            Variables.Clear();
            foreach (var v in Model.Variables.Values) Variables.Add(v);
        }

        /// <summary>그래프에 변수 추가. Phase K — Get/Set 노드는 단일 등록이라 추가 등록 불필요.</summary>
        public GraphVariable AddVariable(string name, Type clrType, object defaultValue)
        {
            var v = Model.AddVariable(name, clrType, defaultValue);
            Variables.Add(v);
            PaletteCategories = _palette.GetCategories();
            GraphVm?.RefreshAllInstancePropertyCandidates();
            StatusMessage = $"Added variable '{name}' (type: {clrType.Name}).";
            return v;
        }

        public bool RemoveVariable(string name)
        {
            if (!Model.RemoveVariable(name)) return false;
            for (int i = Variables.Count - 1; i >= 0; i--)
            {
                if (Variables[i].Name == name) Variables.RemoveAt(i);
            }
            GraphVm?.RefreshAllInstancePropertyCandidates();
            StatusMessage = $"Removed variable '{name}'.";
            return true;
        }

        /// <summary>호스트가 사용자 입력 타입 라벨 → CLR Type 으로 변환. 기본 라벨만 지원, sample 이 override 권장.</summary>
        protected virtual Type ResolveTypeFromLabel(string label) => label switch
        {
            "int" => typeof(int),
            "double" => typeof(double),
            "string" => typeof(string),
            "bool" => typeof(bool),
            "long" => typeof(long),
            _ => null,
        };

        protected static object ParseDefault(Type clrType, string raw)
        {
            if (clrType == typeof(string)) return raw ?? string.Empty;
            if (!clrType.IsValueType) return null;
            if (string.IsNullOrWhiteSpace(raw)) return Activator.CreateInstance(clrType);
            try
            {
                return Convert.ChangeType(raw, clrType, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return Activator.CreateInstance(clrType); }
        }

        /// <summary>사이드바 "Add" — 입력 검증 후 AddVariable 위임.</summary>
        [RelayCommand]
        protected void AddVariableFromSidebar()
        {
            var name = (NewVariableName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name)) { StatusMessage = "Variable name is empty."; return; }
            if (Variables.Any(v => v.Name == name)) { StatusMessage = $"Variable '{name}' already exists."; return; }
            var clrType = ResolveTypeFromLabel(NewVariableTypeName);
            if (clrType == null) { StatusMessage = $"Unknown variable type '{NewVariableTypeName}'."; return; }
            var def = ParseDefault(clrType, NewVariableDefault);
            try
            {
                AddVariable(name, clrType, def);
                NewVariableName = string.Empty;
            }
            catch (InvalidOperationException ex) { StatusMessage = ex.Message; }
        }

        /// <summary>사이드바 행별 "X" — 변수명으로 제거.</summary>
        [RelayCommand]
        protected void RemoveVariableByName(string name)
        {
            if (!string.IsNullOrEmpty(name)) RemoveVariable(name);
        }

        // ============= 팔레트 → 그래프 =============

        /// <summary>팔레트 항목을 캔버스에 드롭했을 때 호출 — 드롭된 캔버스 좌표에 노드 추가.
        /// View 가 드래그-앤-드롭 핸들러에서 ScreenToCanvas 변환 후 호출.
        /// (좌표 미상이면 (320, 200) fallback — 명령 매개변수가 entry 단일이면 이 fallback 으로 동작.)</summary>
        public void AddNodeFromPaletteAt(NodePaletteEntry entry, double x, double y)
        {
            if (entry == null) return;
            try
            {
                var node = Model.AddNode(entry.TypeId, x, y);
                GraphVm.SelectedNode = GraphVm.FindNode(node.Id);
                StatusMessage = $"Added '{entry.DisplayName}' ({entry.TypeId}) at ({x:F0}, {y:F0}).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to add {entry.TypeId}: {ex.Message}";
            }
        }

        /// <summary>하위 호환 + 키보드/접근성용 명령 진입점 — 좌표 fallback (320, 200) 으로 추가.
        /// 일반 GUI 흐름은 드래그-앤-드롭이라 캔버스가 <see cref="AddNodeFromPaletteAt"/> 를 직접 호출.</summary>
        [RelayCommand]
        protected void AddNodeFromPalette(NodePaletteEntry entry) => AddNodeFromPaletteAt(entry, 320, 200);

        // ============= JSON Save / Load =============

        /// <summary>SaveJson — 그래프 + 변수만 저장. Phase M 이후 사용자 코드는 컨테이너 단위 별도 JSON.</summary>
        [RelayCommand]
        protected void SaveJson()
        {
            if (_fileDialog == null) { StatusMessage = "File dialog service not available."; return; }
            try
            {
                var path = _fileDialog.SaveFile("JSON files (*.json)|*.json|All files (*.*)|*.*", suggestedName: $"{DisplayName}.json");
                if (string.IsNullOrEmpty(path)) return;

                var json = NodeGraphSerializer.Serialize(Model);
                File.WriteAllText(path, json, Encoding.UTF8);
                StatusMessage = $"Saved graph to {Path.GetFileName(path)} ({json.Length} chars).";
            }
            catch (Exception ex) { StatusMessage = $"Save failed: {ex.Message}"; }
        }

        /// <summary>LoadJson — 그래프 + 변수만 디시리얼라이즈. 사용자 코드 typeId 는 컨테이너의 Load Code… 가 사전에 등록한 상태 가정.</summary>
        [RelayCommand]
        protected void LoadJson()
        {
            if (_fileDialog == null) { StatusMessage = "File dialog service not available."; return; }
            try
            {
                var path = _fileDialog.OpenFile("JSON files (*.json)|*.json|All files (*.*)|*.*");
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                var json = File.ReadAllText(path, Encoding.UTF8);
                var loaded = NodeGraphSerializer.Deserialize(json);
                Model = loaded;
                GraphVm = CreateGraphVm(Model);
                GraphVm.SelectedNode = GraphVm.Nodes.FirstOrDefault(n => n.TypeId == StartNode.TypeIdConst);
                SyncVariablesFromGraph();
                PaletteCategories = _palette.GetCategories();

                StatusMessage = $"Loaded {Path.GetFileName(path)} — {loaded.Nodes.Count} nodes, {loaded.Connections.Count} connections.";
            }
            catch (Exception ex) { StatusMessage = $"Load failed: {ex.Message}"; }
        }
    }
}
