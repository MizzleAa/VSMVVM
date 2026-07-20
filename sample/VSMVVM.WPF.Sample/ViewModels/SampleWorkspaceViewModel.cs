using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Compilation;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.WPF.Sample.Scheduler;
using VSMVVM.WPF.Scheduler.Services;
using VSMVVM.WPF.Scheduler.ViewModels;
using VSMVVM.WPF.Services;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// Sample 전용 워크스페이스 — generic <see cref="GraphWorkspaceViewModel"/> 에 OpenCV 데모/이미지 미리보기 같은
    /// sample 한정 기능을 더한 서브클래스. 한 탭 = 한 SampleWorkspaceViewModel.
    /// <para>
    /// 추가:
    ///   • <see cref="SnapshotStore"/> — ImageViewNode 가 실행될 때마다 Mat.Clone() 누적. Run 시작 시 자동 클리어.
    ///   • <see cref="VariableTypeOptions"/> — 기본 라벨 + "Mat" 추가.
    ///   • LoadRandomDemo / LoadCollectionDemo / LoadOpenCvDemo — 그래프 + 필요 시 OpenCV snippet 자동 구성.
    ///   • OpenImageViewForNode — ImageViewNode 더블클릭 시 ViewName 별 모덜리스 다이얼로그.
    /// </para>
    /// </summary>
    public partial class SampleWorkspaceViewModel : GraphWorkspaceViewModel
    {
        /// <summary>ImageViewNode 가 실행될 때마다 Mat.Clone() 을 누적 — Run 시작 시 자동 클리어.</summary>
        public ImageSnapshotStore SnapshotStore { get; } = new ImageSnapshotStore();

        /// <summary>ChartViewNode 의 실시간 스트리밍 데이터 저장소 — Run 시작 시 클리어 + ChartLog.Attach.</summary>
        public ChartSnapshotStore ChartStore { get; } = new ChartSnapshotStore();

        /// <summary>현재 열려있는 ImageViewWindow — ViewName 별 1개. 닫히면 제거.</summary>
        private readonly Dictionary<string, System.Windows.Window> _imageViewWindows = new();

        /// <summary>현재 열려있는 ChartViewWindow — ViewName 별 1개.</summary>
        private readonly Dictionary<string, System.Windows.Window> _chartViewWindows = new();

        /// <summary>Sample 만의 변수 타입 옵션 — 기본 + OpenCV Mat.</summary>
        public override IReadOnlyList<string> VariableTypeOptions { get; } =
            new[] { "int", "double", "string", "bool", "long", "Mat" };

        /// <summary>
        /// Phase M — 컨테이너 (사용자 코드 전역 보유) 의 reference. 데모 명령이 컨테이너 Snippets 에 주입 + 컴파일.
        /// 테스트 시나리오에서 컨테이너 없이 워크스페이스만 만들 수 있도록 nullable.
        /// </summary>
        public SchedulerDemoViewModel Container { get; }

        /// <summary>Demo picker 다이얼로그용 — 없으면 관련 명령 비활성.</summary>
        private readonly IDialogService _dialogService;

        public SampleWorkspaceViewModel(
            ICompilationService compiler,
            INodePaletteService palette,
            ISchedulerService scheduler = null,
            IMessenger messenger = null,
            IUndoRedoService undo = null,
            ILoggerService logger = null,
            IFileDialogService fileDialog = null,
            IWindowService windowService = null,
            IDialogService dialogService = null,
            SchedulerDemoViewModel container = null,
            string displayName = "Workspace")
            : base(compiler, palette, scheduler, messenger, undo, logger, fileDialog, windowService, displayName)
        {
            _dialogService = dialogService;
            Container = container;
            // Run 시작 직전: 스냅샷 저장소 클리어 + ctx.Variables 에 주입 + ChartLog attach.
            ConfigureContext = ctx =>
            {
                SnapshotStore.Clear();
                ctx.Variables[ImageViewNode.SnapshotStoreKey] = SnapshotStore;

                ChartStore.Clear();
                ctx.Variables[ChartViewNode.SnapshotStoreKey] = ChartStore;
                ChartLog.Attach(ChartStore);
            };

            // 노드 인스펙터의 "Open Code" 버튼 이벤트를 모든 노드에 훅킹.
            // GraphVm 자체가 RebuildGraph/LoadJson 시 교체되므로 PropertyChanged 로 재구독.
            AttachOpenSourceHooks(GraphVm);
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GraphVm)) AttachOpenSourceHooks(GraphVm);
            };
        }

        private void AttachOpenSourceHooks(NodeGraphViewModel graphVm)
        {
            if (graphVm == null) return;
            foreach (var n in graphVm.Nodes) n.OpenSourceRequested += OnNodeOpenSourceRequested;
            graphVm.Nodes.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (NodeViewModel n in e.NewItems) n.OpenSourceRequested += OnNodeOpenSourceRequested;
                if (e.OldItems != null)
                    foreach (NodeViewModel n in e.OldItems) n.OpenSourceRequested -= OnNodeOpenSourceRequested;
            };
        }

        private void OnNodeOpenSourceRequested(object sender, INode model)
        {
            if (Container == null) { StatusMessage = "Open Code: container not available."; return; }
            var asmName = TryGetSourceAssemblyName(model);
            if (asmName == null) { StatusMessage = "Open Code: this node has no user source."; return; }

            // 어셈블리명 규칙: "UserCode_{Category}" — SchedulerDemoViewModel.CompileAllSnippets 참조.
            const string prefix = "UserCode_";
            string category = asmName.StartsWith(prefix, StringComparison.Ordinal)
                ? asmName.Substring(prefix.Length)
                : null;

            Container.InitialSnippetCategory = category;
            Container.OpenUserCodeEditorCommand.Execute(null);
        }

        private static string TryGetSourceAssemblyName(INode model)
        {
            switch (model)
            {
                case CustomFunctionNode f: return f.Method?.DeclaringType?.Assembly?.GetName()?.Name;
                case CustomConstantNode c: return c.Method?.DeclaringType?.Assembly?.GetName()?.Name;
                case CustomParameterNode p: return p.Field?.DeclaringType?.Assembly?.GetName()?.Name;
                default: return null;
            }
        }

        protected override Type ResolveTypeFromLabel(string label) => label switch
        {
            "Mat" => typeof(Mat),
            _ => base.ResolveTypeFromLabel(label),
        };

        // ============= 다이얼로그 명령 =============

        /// <summary>"Variables…" 버튼 — 그래프 변수 관리 다이얼로그.</summary>
        [RelayCommand]
        private void OpenVariablesManager()
        {
            if (_windowService == null)
            {
                StatusMessage = "WindowService not available — cannot open variables dialog.";
                return;
            }
            // 다이얼로그가 직접 Model.Variables 를 변이 — VM 의 Variables 거울을 그 후 동기화.
            _windowService.ShowWindow<object, NodeGraph>(
                nameof(Views.VariablesManagerWindow), 520, 540, Model);
            SyncVariablesFromGraph();
            RefreshPaletteCategories();
            StatusMessage = "Variables dialog closed.";
        }

        /// <summary>
        /// Phase M — 인스펙터의 "+ Add new variable…" 선택 시 VariablesManagerWindow 모달로 띄움.
        /// 다이얼로그 닫힌 후 가장 마지막에 추가된 변수 이름 반환.
        /// WindowService 가 없으면 base 자동 생성 fallback.
        /// </summary>
        protected override string OnAddNewVariableRequested()
        {
            if (_windowService == null) return base.OnAddNewVariableRequested();

            var beforeNames = new HashSet<string>(Model.Variables.Keys, StringComparer.Ordinal);
            _windowService.ShowWindow<object, NodeGraph>(
                nameof(Views.VariablesManagerWindow), 520, 540, Model);
            SyncVariablesFromGraph();
            RefreshPaletteCategories();

            // 새로 추가된 변수 이름을 찾아 반환 (1개 이상이면 첫 번째).
            foreach (var name in Model.Variables.Keys)
            {
                if (!beforeNames.Contains(name)) return name;
            }
            return null;
        }

        // ============= 데모 선택 다이얼로그 =============

        /// <summary>
        /// "Demo…" 버튼 — DialogService 로 <see cref="Views.SampleDemoPickerView"/> 를 띄우고
        /// 사용자가 고른 <see cref="SampleDemoItem"/> 의 로더를 실행. 모든 개별 데모 버튼을 대체.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanOpenDemoPicker))]
        private void OpenDemoPicker()
        {
            if (_dialogService == null)
            {
                StatusMessage = "DialogService not available — cannot open demo picker.";
                return;
            }

            var items = BuildDemoList();
            var result = _dialogService.ShowDialog<SampleDemoItem, IReadOnlyList<SampleDemoItem>>(
                nameof(Views.SampleDemoPickerView), 520, 440, items,
                DialogButtons.OKCancel, title: "Load Demo");

            if (result.Result != DialogResultType.OK || result.Data == null) return;
            try { result.Data.Load(); }
            catch (Exception ex) { StatusMessage = $"Demo load failed: {ex.Message}"; }
        }

        private bool CanOpenDemoPicker() => _dialogService != null;

        private IReadOnlyList<SampleDemoItem> BuildDemoList() => new[]
        {
            new SampleDemoItem("Random Demo",
                "Start → RandomInt(1..100) → Set 'roll' → Output → End. 변수 쓰기/읽기 흐름 데모.",
                LoadRandomDemo),
            new SampleDemoItem("Fork/Join Demo",
                "Fork(2) 병렬 브랜치 + 각 1s Delay → Join. 병렬이면 총 ≈ 1s, 직렬이면 ≈ 2s. Log 로 육안 확인.",
                LoadForkJoinDemo),
            new SampleDemoItem("Collection Demo",
                "List/Map 조작 노드 시연 — 컬렉션 변수의 라이프사이클.",
                LoadCollectionDemo),
            new SampleDemoItem("Parameter Demo",
                "ParameterNode 정적 필드 3 + MethodNode 1. 코드 에디터에서 필드 값 바꾸면 다음 Run 부터 반영.",
                LoadParameterDemo),
            new SampleDemoItem("Iris Demo",
                "4→hidden→3 MLP 로 Iris 학습 + 80/20 검증. Train/Val Accuracy, Confusion Matrix 표시.",
                LoadIrisDemo),
            new SampleDemoItem("Mnist Demo",
                "784→128→10 MLP 로 MNIST 학습. 첫 Run 시 CSV 다운로드 (~15MB). accuracy/loss/heatmap 창.",
                LoadMnistDemo),
            new SampleDemoItem("OpenCV Demo",
                "OpenCvSharp 스니펫 자동 구성 — ImRead/ImWrite/ImageView 노드로 파이프라인 시연.",
                LoadOpenCvDemo),
            new SampleDemoItem("Log Flood (Stress)",
                "백그라운드 스레드에서 초당 수천 건 LogSink.Write 를 5초간 발화 — 부하 시 UI 응답성 관찰용. " +
                "1.2.18+ VM 은 배치 flush 로 굳지 않아야 함.",
                LoadLogFloodDemo),
        };

        // ============= 데모 그래프 빌더 =============

        /// <summary>"Random Demo" — Start → RandomInt(1..100) → Set 'roll' → Output → End.</summary>
        [RelayCommand]
        private void LoadRandomDemo()
        {
            RebuildGraph(g =>
            {
                if (!g.Variables.ContainsKey("roll"))
                    g.AddVariable("roll", typeof(int), 0);

                var start = g.AddNode(StartNode.TypeIdConst, 80, 200);
                var random = g.AddNode(RandomIntNode.TypeIdConst, 320, 100);
                ((NodeBase)random).SetLiteralInput("Min", 1);
                ((NodeBase)random).SetLiteralInput("Max", 100);
                ((NodeBase)random).SetLiteralInput("Seed", -1);

                var setVar = (SetVariableNode)g.AddNode(SetVariableNode.TypeIdConst, 320, 240);
                setVar.ItemType = typeof(int);
                setVar.VariableName = "roll";

                var getVar = (GetVariableNode)g.AddNode(GetVariableNode.TypeIdConst, 560, 100);
                getVar.ItemType = typeof(int);
                getVar.VariableName = "roll";

                var output = g.AddNode(OutputNode.TypeIdConst, 560, 240);
                ((NodeBase)output).SetLiteralInput("Key", "roll");
                var end = g.AddNode(EndNode.TypeIdConst, 800, 240);

                g.Connect(start.Id, "Then", setVar.Id, "In");
                g.Connect(random.Id, "Out", setVar.Id, "Value");
                g.Connect(setVar.Id, "Then", output.Id, "In");
                g.Connect(getVar.Id, "Value", output.Id, "Value");
                g.Connect(output.Id, "Then", end.Id, "In");
            });
            StatusMessage = "Random demo loaded: Start → RandomInt(1..100) → Set 'roll' → Output → End. Press Run.";
        }

        /// <summary>
        /// "Fork/Join Demo" — Fork 로 두 브랜치를 병렬 실행 후 Join 에서 재합류.
        /// <para>
        /// 그래프: Start → Fork → { Branch0: Delay(1s) → Log("A done") · Branch1: Delay(1s) → Log("B done") } → Join → Log("Both done") → End.
        /// </para>
        /// <para>
        /// 육안 검증 방법 — Run 을 눌러 총 소요 시간을 확인:
        ///   • 병렬 실행되면 총 소요 시간이 ≈ 1 초 (두 Delay 가 동시).
        ///   • 만약 직렬이면 ≈ 2 초 걸릴 것 (Delay 두 번 순차).
        /// 실행 히스토리 패널에서 각 노드 elapsed 와 함께 두 브랜치의 실행 시각이 겹치는 것을 볼 수 있음.
        /// </para>
        /// </summary>
        [RelayCommand]
        private void LoadForkJoinDemo()
        {
            RebuildGraph(g =>
            {
                // 레이아웃: 세 컬럼(Fork, 각 브랜치, Join) — 위·아래 두 브랜치가 시각적으로 대칭.
                const double startX = 60;
                const double forkX = 260;
                const double branchX = 460;
                const double joinX = 720;
                const double afterX = 900;
                const double endX = 1100;
                const double topY = 100;
                const double midY = 200;
                const double botY = 300;

                var start = g.AddNode(StartNode.TypeIdConst, startX, midY);
                var fork = g.AddNode(ForkNode.TypeIdConst, forkX, midY);

                var delayA = g.AddNode(DelayNode.TypeIdConst, branchX, topY);
                ((NodeBase)delayA).SetLiteralInput("Seconds", 1.0);
                var logA = g.AddNode(LogNode.TypeIdConst, branchX + 180, topY);
                ((NodeBase)logA).SetLiteralInput("Format", "Branch A done");

                var delayB = g.AddNode(DelayNode.TypeIdConst, branchX, botY);
                ((NodeBase)delayB).SetLiteralInput("Seconds", 1.0);
                var logB = g.AddNode(LogNode.TypeIdConst, branchX + 180, botY);
                ((NodeBase)logB).SetLiteralInput("Format", "Branch B done");

                var join = g.AddNode(JoinNode.TypeIdConst, joinX, midY);
                var logJoined = g.AddNode(LogNode.TypeIdConst, afterX, midY);
                ((NodeBase)logJoined).SetLiteralInput("Format", "Both branches done — Join fired");
                var end = g.AddNode(EndNode.TypeIdConst, endX, midY);

                g.Connect(start.Id, "Then", fork.Id, "In");
                g.Connect(fork.Id, "Branch0", delayA.Id, "In");
                g.Connect(fork.Id, "Branch1", delayB.Id, "In");
                g.Connect(delayA.Id, "Then", logA.Id, "In");
                g.Connect(delayB.Id, "Then", logB.Id, "In");
                g.Connect(logA.Id, "Then", join.Id, "In0");
                g.Connect(logB.Id, "Then", join.Id, "In1");
                g.Connect(join.Id, "Then", logJoined.Id, "In");
                g.Connect(logJoined.Id, "Then", end.Id, "In");
            });
            StatusMessage = "Fork/Join demo loaded: Start → Fork(2) → 두 Delay(1s) → Join → Log → End. " +
                            "Run 시 병렬이면 총 ≈ 1s, 직렬이면 ≈ 2s. Log 패널에서 두 브랜치 완료 순서와 히스토리의 elapsed 를 관찰하세요.";
        }

        /// <summary>
        /// "Collection Demo" — Phase L: ParameterNode 정적 필드로 List&lt;double&gt; 정의 →
        /// SetVariable("nums") → GetVariable → Output("nums") → End. 사용자가 코드 에디터의 필드값을
        /// 수정 후 재컴파일하면 즉시 그래프에 반영.
        /// </summary>
        [RelayCommand]
        private void LoadCollectionDemo()
        {
            if (Container == null) { StatusMessage = "Collection demo: container not available."; return; }

            // 1) Parameters 조각 주입/교체 + 컨테이너 컴파일 → CustomParameterNode 등록.
            var existing = Container.Snippets.FirstOrDefault(s => s.Category == "Parameters");
            if (existing != null)
            {
                existing.SourceCode = CollectionDemoSource;
            }
            else
            {
                Container.Snippets.Add(new UserCodeSnippet("Parameters", CollectionDemoSource));
            }
            Container.CompileAllSnippetsCommand.Execute(null);

            // 데모 예제 스펙 — (파라미터 노드 TypeId, 출력 키/변수명, CLR 타입)
            var demos = new (string TypeId, string Key, Type ClrType)[]
            {
                ("Demo.Params.Nums",     "nums",     typeof(List<double>)),
                ("Demo.Params.Ints",     "ints",     typeof(List<int>)),
                ("Demo.Params.Names",    "names",    typeof(List<string>)),
                ("Demo.Params.Matrix2D", "matrix",   typeof(List<List<int>>)),
                ("Demo.Params.Cube3D",   "cube",     typeof(List<List<List<double>>>)),
                ("Demo.Params.Scores",   "scores",   typeof(Dictionary<string, int>)),
            };

            var missing = demos.FirstOrDefault(d => NodeMetadataRegistry.Get(d.TypeId) == null);
            if (missing.TypeId != null)
            {
                StatusMessage = $"Collection demo: compile OK but [ParameterNode(\"{missing.TypeId}\")] not found.";
                return;
            }

            // 2) 그래프 빌드 — 각 파라미터 노드마다 SetVariable → Output 체인을 나란히 배치.
            //    Start → set[0] → set[1] → ... → set[N-1] → output[0..N-1] → End.
            RebuildGraph(g =>
            {
                foreach (var d in demos)
                {
                    if (!g.Variables.ContainsKey(d.Key))
                        g.AddVariable(d.Key, d.ClrType, System.Activator.CreateInstance(d.ClrType));
                }

                const double rowHeight = 140;
                const double paramX = 80;
                const double setX = 340;
                const double outputX = 600;
                const double endX = 860;

                var start = g.AddNode(StartNode.TypeIdConst, paramX, 40);
                var end = g.AddNode(EndNode.TypeIdConst, endX, 40 + rowHeight * (demos.Length - 1) / 2.0);

                var prevExitNodeId = start.Id;
                var prevExitPin = "Then";

                for (int i = 0; i < demos.Length; i++)
                {
                    var (typeId, key, clrType) = demos[i];
                    double y = 120 + i * rowHeight;

                    var param = g.AddNode(typeId, paramX, y);

                    var setVar = (SetVariableNode)g.AddNode(SetVariableNode.TypeIdConst, setX, y);
                    setVar.ItemType = clrType;
                    setVar.VariableName = key;

                    var output = g.AddNode(OutputNode.TypeIdConst, outputX, y);
                    ((NodeBase)output).SetLiteralInput("Key", key);

                    var getVar = (GetVariableNode)g.AddNode(GetVariableNode.TypeIdConst, outputX - 200, y - 70);
                    getVar.ItemType = clrType;
                    getVar.VariableName = key;

                    g.Connect(prevExitNodeId, prevExitPin, setVar.Id, "In");
                    g.Connect(param.Id, "Out", setVar.Id, "Value");
                    g.Connect(setVar.Id, "Then", output.Id, "In");
                    g.Connect(getVar.Id, "Value", output.Id, "Value");

                    prevExitNodeId = output.Id;
                    prevExitPin = "Then";
                }

                g.Connect(prevExitNodeId, prevExitPin, end.Id, "In");
            });
            StatusMessage = "Collection demo loaded: 6 ParameterNode (1D/2D/3D/Dictionary) → Set/Output. Press Run then click nodes.";
        }

        /// <summary>
        /// Phase L Collection 데모용 코드 조각 — [ParameterNode] 정적 필드 시연.
        /// 다양한 자료형(double/int/string) + 다차원(1D/2D/3D) + Dictionary 예제.
        /// 사용자가 에디터에서 자유 편집 가능.
        /// </summary>
        private const string CollectionDemoSource = @"using System;
using System.Collections.Generic;
using VSMVVM.Core.Scheduler.Attributes;

public static class CollectionDemoParams
{
    // 1D
    [ParameterNode(""Demo.Params.Nums"", Category = ""Parameters"", DisplayName = ""Nums (List<double>)"")]
    public static List<double> Nums = new() { 1.0, 2.5, 3.5, 4.0 };

    [ParameterNode(""Demo.Params.Ints"", Category = ""Parameters"", DisplayName = ""Ints (List<int>)"")]
    public static List<int> Ints = new() { 10, 20, 30, 40, 50 };

    [ParameterNode(""Demo.Params.Names"", Category = ""Parameters"", DisplayName = ""Names (List<string>)"")]
    public static List<string> Names = new() { ""alpha"", ""bravo"", ""charlie"", ""delta"" };

    // 2D (jagged)
    [ParameterNode(""Demo.Params.Matrix2D"", Category = ""Parameters"", DisplayName = ""Matrix2D (List<List<int>>)"")]
    public static List<List<int>> Matrix2D = new()
    {
        new() { 1, 2, 3 },
        new() { 4, 5, 6 },
        new() { 7, 8, 9 },
    };

    // 3D (jagged) — 2×2×3 double 큐브
    [ParameterNode(""Demo.Params.Cube3D"", Category = ""Parameters"", DisplayName = ""Cube3D (List<List<List<double>>>)"")]
    public static List<List<List<double>>> Cube3D = new()
    {
        new()
        {
            new() { 1.0, 1.1, 1.2 },
            new() { 1.3, 1.4, 1.5 },
        },
        new()
        {
            new() { 2.0, 2.1, 2.2 },
            new() { 2.3, 2.4, 2.5 },
        },
    };

    // Dictionary
    [ParameterNode(""Demo.Params.Scores"", Category = ""Parameters"", DisplayName = ""Scores (Dictionary<string,int>)"")]
    public static Dictionary<string, int> Scores = new()
    {
        { ""alice"", 92 },
        { ""bob"", 78 },
        { ""carol"", 85 },
    };
}
";

        /// <summary>
        /// Phase L "Parameter Demo" — [ParameterNode] 와 [MethodNode] 가 함께 동작하는 시나리오.
        /// 정적 필드 3개 (PlayerName/Score/IsWinner) 를 ParameterNode 로 노출 + FormatReport 메서드를 MethodNode 로 노출
        /// → 그래프가 세 파라미터를 끌어와 한 문자열로 조립 → Output.
        /// 사용자가 코드 에디터에서 정적 필드 값을 바꾸고 재컴파일하면 다음 Run 부터 즉시 새 값이 반영됨.
        /// </summary>
        [RelayCommand]
        private void LoadParameterDemo()
        {
            if (Container == null) { StatusMessage = "Parameter demo: container not available."; return; }

            // 1) Parameters 조각 주입/교체 + 컨테이너 컴파일 → ParameterNode/MethodNode 4종 등록.
            var existing = Container.Snippets.FirstOrDefault(s => s.Category == "PlayerStats");
            if (existing != null)
            {
                existing.SourceCode = ParameterDemoSource;
            }
            else
            {
                Container.Snippets.Add(new UserCodeSnippet("PlayerStats", ParameterDemoSource));
            }
            Container.CompileAllSnippetsCommand.Execute(null);

            string[] requiredIds =
            {
                "Demo.Player.Name", "Demo.Player.Score", "Demo.Player.IsWinner",
                "Demo.Player.FormatReport", "Demo.Player.GetParameters",
            };
            foreach (var id in requiredIds)
            {
                if (NodeMetadataRegistry.Get(id) == null)
                {
                    StatusMessage = $"Parameter demo: compile OK but '{id}' not found.";
                    return;
                }
            }

            // 2) 그래프 빌드 — 두 라인:
            //   상단: Start → FormatReport(name,score,isWinner) → Output("report") → End.
            //   하단: GetParameters() 튜플 → Output("player.name" / "player.score" / "player.isWinner") 3개.
            RebuildGraph(g =>
            {
                var start = g.AddNode(StartNode.TypeIdConst, 80, 240);
                var paramName = g.AddNode("Demo.Player.Name", 80, 100);
                var paramScore = g.AddNode("Demo.Player.Score", 80, 200);
                var paramWinner = g.AddNode("Demo.Player.IsWinner", 80, 300);

                var format = g.AddNode("Demo.Player.FormatReport", 360, 200);
                var output = g.AddNode(OutputNode.TypeIdConst, 640, 200);
                ((NodeBase)output).SetLiteralInput("Key", "report");

                g.Connect(start.Id, "Then", format.Id, "In");
                g.Connect(paramName.Id, "Out", format.Id, "name");
                g.Connect(paramScore.Id, "Out", format.Id, "score");
                g.Connect(paramWinner.Id, "Out", format.Id, "isWinner");
                g.Connect(format.Id, "Result", output.Id, "Value");
                g.Connect(format.Id, "Then", output.Id, "In");

                // 튜플 리턴 노드 — constant-like (Exec 핀 없음). Output 3개가 각 요소를 소비.
                var getParams = g.AddNode("Demo.Player.GetParameters", 360, 460);

                var outName = g.AddNode(OutputNode.TypeIdConst, 640, 400);
                ((NodeBase)outName).SetLiteralInput("Key", "player.name");
                var outScore = g.AddNode(OutputNode.TypeIdConst, 640, 480);
                ((NodeBase)outScore).SetLiteralInput("Key", "player.score");
                var outWinner = g.AddNode(OutputNode.TypeIdConst, 640, 560);
                ((NodeBase)outWinner).SetLiteralInput("Key", "player.isWinner");

                g.Connect(getParams.Id, "Name", outName.Id, "Value");
                g.Connect(getParams.Id, "Score", outScore.Id, "Value");
                g.Connect(getParams.Id, "IsWinner", outWinner.Id, "Value");

                // 실행 흐름 이어붙이기: FormatReport Output → 튜플 Output 3개 → End.
                var end = g.AddNode(EndNode.TypeIdConst, 880, 480);
                g.Connect(output.Id, "Then", outName.Id, "In");
                g.Connect(outName.Id, "Then", outScore.Id, "In");
                g.Connect(outScore.Id, "Then", outWinner.Id, "In");
                g.Connect(outWinner.Id, "Then", end.Id, "In");
            });
            StatusMessage = "Parameter demo loaded: FormatReport + GetParameters (tuple → Name/Score/IsWinner pins). Press Run.";
        }

        /// <summary>
        /// Phase L Parameter 데모용 코드 조각 — [ParameterNode] 3개 + [MethodNode] 1개.
        /// 정적 필드는 그래프 실행 사이에 값을 유지 (어셈블리 메모리에 상주).
        /// </summary>
        private const string ParameterDemoSource = @"using VSMVVM.Core.Scheduler.Attributes;

public static class PlayerStats
{
    [ParameterNode(""Demo.Player.Name"", Category = ""PlayerStats"", DisplayName = ""Player Name"")]
    public static string Name = ""Alice"";

    [ParameterNode(""Demo.Player.Score"", Category = ""PlayerStats"", DisplayName = ""Score"")]
    public static int Score = 1250;

    [ParameterNode(""Demo.Player.IsWinner"", Category = ""PlayerStats"", DisplayName = ""Is Winner"")]
    public static bool IsWinner = true;

    [MethodNode(""Demo.Player.FormatReport"", Category = ""PlayerStats"", DisplayName = ""Format Report"")]
    public static string FormatReport(string name, int score, bool isWinner)
    {
        var trophy = isWinner ? "" 🏆"" : """";
        return $""{name}: {score} pts{trophy}"";
    }

    // 튜플 반환 — 요소 이름이 그대로 출력 핀 이름(Name/Score/IsWinner)이 된다.
    [MethodNode(""Demo.Player.GetParameters"", Category = ""PlayerStats"", DisplayName = ""Get Parameters"")]
    public static (string Name, int Score, bool IsWinner) GetParameters()
    {
        return (Name, Score, IsWinner);
    }
}
";

        /// <summary>
        /// "Iris Demo" — Iris 데이터셋으로 4→4→3 MLP 학습 + 검증 (80/20 stratified split).
        /// CSV 파일은 Sample 프로젝트 리소스 Data\iris.csv 에서 자동 로드.
        /// 한 번의 Run 이 전체 학습(Epochs 만큼) + 검증 → Train/Val Accuracy, Final Loss, Confusion Matrix (3×3),
        /// Per-class Accuracy 를 각 Output 노드에 표시. 인스펙터 "자세히" 창으로 혼동행렬/가중치 드릴다운 가능.
        /// </summary>
        [RelayCommand]
        private void LoadIrisDemo()
        {
            if (Container == null) { StatusMessage = "Iris demo: container not available."; return; }

            var existing = Container.Snippets.FirstOrDefault(s => s.Category == "Iris");
            if (existing != null)
            {
                existing.SourceCode = IrisDemoSource;
            }
            else
            {
                Container.Snippets.Add(new UserCodeSnippet("Iris", IrisDemoSource));
            }
            Container.CompileAllSnippetsCommand.Execute(null);

            string[] requiredIds =
            {
                "Iris.LearningRate", "Iris.HiddenSize", "Iris.Epochs",
                "Iris.LoadCsv", "Iris.StratifiedSplit",
                "Iris.InitState", "Iris.TrainOneEpoch", "Iris.Evaluate",
            };
            foreach (var id in requiredIds)
            {
                if (NodeMetadataRegistry.Get(id) == null)
                {
                    StatusMessage = $"Iris demo: compile OK but '{id}' not found.";
                    return;
                }
            }

            RebuildGraph(g =>
            {
                // 그래프 변수 — IrisState 를 object 로 담음.
                if (!g.Variables.ContainsKey("state"))
                    g.AddVariable("state", typeof(object), null);

                var start = g.AddNode(StartNode.TypeIdConst, 40, 40);

                // === 데이터 준비 파이프라인 ===
                var load = g.AddNode("Iris.LoadCsv", 40, 160);
                var split = g.AddNode("Iris.StratifiedSplit", 260, 160);
                g.Connect(load.Id, "X", split.Id, "X");
                g.Connect(load.Id, "Y", split.Id, "Y");

                var initState = g.AddNode("Iris.InitState", 480, 160);
                g.Connect(split.Id, "Xtr",  initState.Id, "Xtr");
                g.Connect(split.Id, "Ytr",  initState.Id, "Ytr");
                g.Connect(split.Id, "Xval", initState.Id, "Xval");
                g.Connect(split.Id, "Yval", initState.Id, "Yval");

                var setStateInit = (SetVariableNode)g.AddNode(SetVariableNode.TypeIdConst, 700, 160);
                setStateInit.ItemType = typeof(object);
                setStateInit.VariableName = "state";
                g.Connect(initState.Id, "Result", setStateInit.Id, "Value");

                // === Repeat 루프 (Count = Epochs) ===
                var epochs = g.AddNode("Iris.Epochs", 40, 340);
                var repeat = g.AddNode(RepeatNode.TypeIdConst, 940, 200);
                g.Connect(epochs.Id, "Out", repeat.Id, "Count");

                // Body: GetVar(state) → TrainOneEpoch → SetVar(state) → Delay → 다시 Repeat.In
                var getStateBody = (GetVariableNode)g.AddNode(GetVariableNode.TypeIdConst, 1160, 100);
                getStateBody.ItemType = typeof(object);
                getStateBody.VariableName = "state";

                var lr = g.AddNode("Iris.LearningRate", 1160, 300);

                var train = g.AddNode("Iris.TrainOneEpoch", 1380, 200);
                g.Connect(getStateBody.Id, "Value", train.Id, "stateObj");
                g.Connect(repeat.Id,       "Index", train.Id, "epoch");
                g.Connect(lr.Id,           "Out",   train.Id, "lr");

                var setStateBody = (SetVariableNode)g.AddNode(SetVariableNode.TypeIdConst, 1620, 200);
                setStateBody.ItemType = typeof(object);
                setStateBody.VariableName = "state";
                g.Connect(train.Id, "Result", setStateBody.Id, "Value");

                // 루프 안의 ChartView 앵커 — 매 에폭마다 실행되어 시각적으로 반짝임 (실시간 갱신은 ChartLog 로 이미 됨).
                var chartAccLoop = (ChartViewNode)g.AddNode(ChartViewNode.TypeIdConst, 1840, 140);
                chartAccLoop.ViewName = "accuracy"; chartAccLoop.Kind = ChartKind.Line;

                var chartLossLoop = (ChartViewNode)g.AddNode(ChartViewNode.TypeIdConst, 1840, 240);
                chartLossLoop.ViewName = "loss"; chartLossLoop.Kind = ChartKind.Line;

                var delay = g.AddNode(DelayNode.TypeIdConst, 2060, 200);
                ((NodeBase)delay).SetLiteralInput("Seconds", 0.2);

                // Body → setStateBody → chartAccLoop → chartLossLoop → Delay → 백엣지 → Repeat.In
                g.Connect(repeat.Id,        "Body", setStateBody.Id,  "In");
                g.Connect(setStateBody.Id,  "Then", chartAccLoop.Id,  "In");
                g.Connect(chartAccLoop.Id,  "Then", chartLossLoop.Id, "In");
                g.Connect(chartLossLoop.Id, "Then", delay.Id,         "In");
                g.Connect(delay.Id,         "Then", repeat.Id,        "In");

                // Done: 최종 Evaluate + Output + Charts.
                var getStateEval = (GetVariableNode)g.AddNode(GetVariableNode.TypeIdConst, 940, 440);
                getStateEval.ItemType = typeof(object);
                getStateEval.VariableName = "state";

                var evalNode = g.AddNode("Iris.Evaluate", 1160, 440);
                g.Connect(getStateEval.Id, "Value", evalNode.Id, "stateObj");

                // 스칼라 Output 5개 + 히스토리 스토리 없음 (매 에폭 push 방식이라 필요 시 별도 List 저장 가능).
                var outTrainAcc = g.AddNode(OutputNode.TypeIdConst, 1380, 380);
                ((NodeBase)outTrainAcc).SetLiteralInput("Key", "train_accuracy");
                var outValAcc = g.AddNode(OutputNode.TypeIdConst, 1380, 450);
                ((NodeBase)outValAcc).SetLiteralInput("Key", "val_accuracy");
                var outConfMat = g.AddNode(OutputNode.TypeIdConst, 1380, 520);
                ((NodeBase)outConfMat).SetLiteralInput("Key", "confusion");
                var outPerClass = g.AddNode(OutputNode.TypeIdConst, 1380, 590);
                ((NodeBase)outPerClass).SetLiteralInput("Key", "per_class_acc");
                var outPerClassCounts = g.AddNode(OutputNode.TypeIdConst, 1380, 660);
                ((NodeBase)outPerClassCounts).SetLiteralInput("Key", "per_class_counts");

                g.Connect(evalNode.Id, "TrainAccuracy",    outTrainAcc.Id,       "Value");
                g.Connect(evalNode.Id, "ValAccuracy",      outValAcc.Id,         "Value");
                g.Connect(evalNode.Id, "Confusion",        outConfMat.Id,        "Value");
                g.Connect(evalNode.Id, "PerClassAccuracy", outPerClass.Id,       "Value");
                g.Connect(evalNode.Id, "PerClassCounts",   outPerClassCounts.Id, "Value");

                // 학습 완료 후 confusion 관점 ChartView 2개.
                var chartConf = (ChartViewNode)g.AddNode(ChartViewNode.TypeIdConst, 1620, 540);
                chartConf.ViewName = "confusion"; chartConf.Kind = ChartKind.ConfusionMatrix;

                var chartTpFn = (ChartViewNode)g.AddNode(ChartViewNode.TypeIdConst, 1620, 620);
                chartTpFn.ViewName = "confusion_tpfn"; chartTpFn.Kind = ChartKind.ConfusionMatrix;

                var end = g.AddNode(EndNode.TypeIdConst, 1880, 580);

                // Exec 체인 (Done): Repeat.Done → 스칼라 Output 5 → confusion charts → End.
                g.Connect(start.Id,             "Then", setStateInit.Id,       "In");
                g.Connect(setStateInit.Id,      "Then", repeat.Id,             "In");
                g.Connect(repeat.Id,            "Done", outTrainAcc.Id,        "In");
                g.Connect(outTrainAcc.Id,       "Then", outValAcc.Id,          "In");
                g.Connect(outValAcc.Id,         "Then", outConfMat.Id,         "In");
                g.Connect(outConfMat.Id,        "Then", outPerClass.Id,        "In");
                g.Connect(outPerClass.Id,       "Then", outPerClassCounts.Id,  "In");
                g.Connect(outPerClassCounts.Id, "Then", chartConf.Id,          "In");
                g.Connect(chartConf.Id,         "Then", chartTpFn.Id,          "In");
                g.Connect(chartTpFn.Id,         "Then", end.Id,                "In");
            });
            StatusMessage = "Iris demo loaded. Double-click chart nodes to open windows, then press Run — Repeat loop drives one epoch per iteration with 200ms Delay for real-time observation.";
        }

        /// <summary>
        /// Phase L Iris 데모용 코드 조각 — 순수 C# 로 4→4→3 MLP 구현.
        /// Epochs 만큼 자동 반복, 80/20 stratified split, 검증 후 혼동행렬/per-class accuracy 반환.
        /// 사용자가 에디터에서 자유 편집 가능 (LearningRate/HiddenSize/Epochs 튜닝, tanh→relu 교체 등).
        /// </summary>
        private const string IrisDemoSource = @"using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VSMVVM.Core.Scheduler.Attributes;
using VSMVVM.WPF.Sample.Scheduler;

// 학습 상태 — 가중치 + 표준화 계수 + 데이터. 그래프 변수 하나로 유지되며 매 에폭마다 갱신.
public sealed class IrisState
{
    public double[][] Xtr; public int[] Ytr;
    public double[][] Xval; public int[] Yval;
    public double[][] W1; public double[] b1;
    public double[][] W2; public double[] b2;
}

public static class IrisModel
{
    [ParameterNode(""Iris.LearningRate"", Category = ""Iris"", DisplayName = ""Learning Rate"")]
    public static double LearningRate = 0.05;

    [ParameterNode(""Iris.HiddenSize"", Category = ""Iris"", DisplayName = ""Hidden Size"")]
    public static int HiddenSize = 8;

    [ParameterNode(""Iris.Epochs"", Category = ""Iris"", DisplayName = ""Epochs"")]
    public static int Epochs = 100;

    // Iris CSV 로더 — Sample 프로젝트 리소스 Data\iris.csv 를 AppContext.BaseDirectory 기준으로 로드.
    [MethodNode(""Iris.LoadCsv"", Category = ""Iris"", DisplayName = ""Load CSV"")]
    public static (double[][] X, int[] Y) LoadCsv()
    {
        var path = Path.Combine(AppContext.BaseDirectory, ""Data"", ""iris.csv"");
        var lines = File.ReadAllLines(path);
        var xs = new List<double[]>();
        var ys = new List<int>();
        var labelMap = new Dictionary<string, int>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(',');
            if (parts.Length < 5) continue;
            if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out _))
                continue;
            var feat = new double[4];
            for (int i = 0; i < 4; i++)
                feat[i] = double.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
            var label = parts[4].Trim();
            if (!labelMap.TryGetValue(label, out var idx))
            {
                idx = labelMap.Count;
                labelMap[label] = idx;
            }
            xs.Add(feat);
            ys.Add(idx);
        }
        return (xs.ToArray(), ys.ToArray());
    }

    // 80/20 stratified split — 클래스별로 80% 학습 / 20% 검증.
    [MethodNode(""Iris.StratifiedSplit"", Category = ""Iris"", DisplayName = ""Stratified Split 80/20"")]
    public static (double[][] Xtr, int[] Ytr, double[][] Xval, int[] Yval)
        StratifiedSplit(double[][] X, int[] Y)
    {
        var rng = new Random(1234);
        var byClass = new Dictionary<int, List<int>>();
        for (int i = 0; i < Y.Length; i++)
        {
            if (!byClass.TryGetValue(Y[i], out var list)) { list = new List<int>(); byClass[Y[i]] = list; }
            list.Add(i);
        }
        var trainIdx = new List<int>();
        var valIdx = new List<int>();
        foreach (var kv in byClass)
        {
            var indices = kv.Value;
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
            int nVal = indices.Count / 5;
            for (int i = 0; i < indices.Count; i++)
                (i < nVal ? valIdx : trainIdx).Add(indices[i]);
        }
        double[][] Take(List<int> ids) => ids.Select(i => X[i]).ToArray();
        int[] TakeY(List<int> ids) => ids.Select(i => Y[i]).ToArray();
        return (Take(trainIdx), TakeY(trainIdx), Take(valIdx), TakeY(valIdx));
    }

    // 학습 상태 초기화 — 표준화 후 XtrN/XvalN + Xavier 가중치 조립해 IrisState 반환.
    // 반환 타입을 object 로 둬서 그래프 변수 (object) 와 호환.
    [MethodNode(""Iris.InitState"", Category = ""Iris"", DisplayName = ""Init State"")]
    public static object InitState(double[][] Xtr, int[] Ytr, double[][] Xval, int[] Yval)
    {
        int inDim = 4;
        int n = Xtr.Length;
        var mean = new double[inDim];
        var std = new double[inDim];
        for (int j = 0; j < inDim; j++)
        {
            double s = 0; for (int i = 0; i < n; i++) s += Xtr[i][j]; mean[j] = s / n;
            double v = 0; for (int i = 0; i < n; i++) { var d = Xtr[i][j] - mean[j]; v += d * d; }
            std[j] = Math.Sqrt(v / n) + 1e-8;
        }
        double[][] Normalize(double[][] src)
        {
            var res = new double[src.Length][];
            for (int i = 0; i < src.Length; i++)
            {
                res[i] = new double[inDim];
                for (int j = 0; j < inDim; j++) res[i][j] = (src[i][j] - mean[j]) / std[j];
            }
            return res;
        }
        var (W1, b1, W2, b2) = InitWeights(42);
        return new IrisState
        {
            Xtr = Normalize(Xtr), Ytr = Ytr,
            Xval = Normalize(Xval), Yval = Yval,
            W1 = W1, b1 = b1, W2 = W2, b2 = b2,
        };
    }

    // 한 에폭 학습 — 상태를 in-place 로 갱신 (같은 참조 반환). ChartLog 로 실시간 스트리밍.
    // 그래프에서 Repeat 노드가 매 반복마다 이 노드를 호출 → Delay 노드로 각 에폭 사이 딜레이.
    // 파라미터/반환 타입을 object 로 둬서 그래프 변수 (object) 와 호환.
    [MethodNode(""Iris.TrainOneEpoch"", Category = ""Iris"", DisplayName = ""Train One Epoch"")]
    public static object TrainOneEpoch(object stateObj, int epoch, double lr)
    {
        var state = (IrisState)stateObj;
        int inDim = 4, hid = HiddenSize, outDim = 3;
        var Xn = state.Xtr; var Y = state.Ytr;
        var W1 = state.W1; var b1 = state.b1;
        var W2 = state.W2; var b2 = state.b2;
        int n = Xn.Length;

        var rng = new Random(epoch * 7919 + 13);
        var idx = Enumerable.Range(0, n).ToArray();
        for (int i = n - 1; i > 0; i--) { int j = rng.Next(i + 1); (idx[i], idx[j]) = (idx[j], idx[i]); }

        double totalLoss = 0;
        for (int step = 0; step < n; step++)
        {
            int k = idx[step];
            var x = Xn[k];
            int y = Y[k];

            var h = new double[hid];
            for (int j = 0; j < hid; j++)
            {
                double s = b1[j];
                for (int i = 0; i < inDim; i++) s += x[i] * W1[i][j];
                h[j] = Math.Tanh(s);
            }
            var logits = new double[outDim];
            for (int j = 0; j < outDim; j++)
            {
                double s = b2[j];
                for (int i = 0; i < hid; i++) s += h[i] * W2[i][j];
                logits[j] = s;
            }
            double m = logits.Max();
            var probs = new double[outDim];
            double sumExp = 0;
            for (int j = 0; j < outDim; j++) { probs[j] = Math.Exp(logits[j] - m); sumExp += probs[j]; }
            for (int j = 0; j < outDim; j++) probs[j] /= sumExp;
            totalLoss += -Math.Log(Math.Max(probs[y], 1e-12));

            var dlogits = new double[outDim];
            for (int j = 0; j < outDim; j++) dlogits[j] = probs[j] - (j == y ? 1.0 : 0.0);
            var dh = new double[hid];
            for (int i = 0; i < hid; i++)
            {
                double s = 0;
                for (int j = 0; j < outDim; j++) s += dlogits[j] * W2[i][j];
                dh[i] = s * (1 - h[i] * h[i]);
            }
            for (int i = 0; i < hid; i++)
                for (int j = 0; j < outDim; j++)
                    W2[i][j] -= lr * dlogits[j] * h[i];
            for (int j = 0; j < outDim; j++) b2[j] -= lr * dlogits[j];
            for (int i = 0; i < inDim; i++)
                for (int j = 0; j < hid; j++)
                    W1[i][j] -= lr * dh[j] * x[i];
            for (int j = 0; j < hid; j++) b1[j] -= lr * dh[j];
        }
        double epochLoss = totalLoss / n;

        double trAcc = AccuracyOn(state, state.Xtr, state.Ytr);
        double vaAcc = AccuracyOn(state, state.Xval, state.Yval);

        // 실시간 스트리밍 → ChartViewWindow 가 즉시 갱신.
        ChartLog.Push(""accuracy"", epoch, ""trainAcc"", trAcc);
        ChartLog.Push(""accuracy"", epoch, ""valAcc"", vaAcc);
        ChartLog.Push(""loss"", epoch, ""loss"", epochLoss);

        return state;
    }

    // 학습 완료 후 최종 검증 — 스칼라 지표 + 두 종류의 혼동행렬 반환 + ChartLog PushMatrix.
    [MethodNode(""Iris.Evaluate"", Category = ""Iris"", DisplayName = ""Evaluate"")]
    public static (double TrainAccuracy, double ValAccuracy,
                   int[][] Confusion, double[] PerClassAccuracy, int[][] PerClassCounts)
        Evaluate(object stateObj)
    {
        var state = (IrisState)stateObj;
        int outDim = 3;
        var classNames = new[] { ""setosa"", ""versicolor"", ""virginica"" };
        var tpFnLabels = new[] { ""TP"", ""TN"", ""FP"", ""FN"" };

        var trainPreds = Predict(state, state.Xtr);
        var valPreds = Predict(state, state.Xval);
        double trainAcc = AccuracyFromPreds(trainPreds, state.Ytr);
        double valAcc = AccuracyFromPreds(valPreds, state.Yval);

        var conf = new int[outDim][];
        for (int i = 0; i < outDim; i++) conf[i] = new int[outDim];
        for (int i = 0; i < state.Yval.Length; i++) conf[state.Yval[i]][valPreds[i]]++;

        var perClass = new double[outDim];
        for (int i = 0; i < outDim; i++)
        {
            int rowSum = 0; for (int j = 0; j < outDim; j++) rowSum += conf[i][j];
            perClass[i] = rowSum == 0 ? 0 : conf[i][i] / (double)rowSum;
        }

        int total = state.Yval.Length;
        var perClassCounts = new int[outDim][];
        for (int c = 0; c < outDim; c++)
        {
            int tp = conf[c][c];
            int fn = 0; for (int j = 0; j < outDim; j++) fn += conf[c][j]; fn -= tp;
            int fp = 0; for (int i = 0; i < outDim; i++) fp += conf[i][c]; fp -= tp;
            int tn = total - tp - fn - fp;
            perClassCounts[c] = new[] { tp, tn, fp, fn };
        }

        ChartLog.PushMatrix(""confusion"", conf, classNames, classNames, ""Actual"", ""Predicted"");
        ChartLog.PushMatrix(""confusion_tpfn"", perClassCounts, classNames, tpFnLabels, ""Class"", ""Metric"");

        return (trainAcc, valAcc, conf, perClass, perClassCounts);
    }

    // === 헬퍼 ===
    private static int[] Predict(IrisState s, double[][] Xn)
    {
        int hid = HiddenSize, inDim = 4, outDim = 3;
        var preds = new int[Xn.Length];
        for (int k = 0; k < Xn.Length; k++)
        {
            var x = Xn[k];
            var hv = new double[hid];
            for (int j = 0; j < hid; j++)
            {
                double sv = s.b1[j];
                for (int i = 0; i < inDim; i++) sv += x[i] * s.W1[i][j];
                hv[j] = Math.Tanh(sv);
            }
            double bestLogit = double.NegativeInfinity; int bestJ = 0;
            for (int j = 0; j < outDim; j++)
            {
                double sv = s.b2[j];
                for (int i = 0; i < hid; i++) sv += hv[i] * s.W2[i][j];
                if (sv > bestLogit) { bestLogit = sv; bestJ = j; }
            }
            preds[k] = bestJ;
        }
        return preds;
    }

    private static double AccuracyOn(IrisState s, double[][] X, int[] Y)
        => AccuracyFromPreds(Predict(s, X), Y);

    private static double AccuracyFromPreds(int[] preds, int[] labels)
    {
        int correct = 0;
        for (int i = 0; i < preds.Length; i++) if (preds[i] == labels[i]) correct++;
        return correct / (double)preds.Length;
    }

    // Xavier-ish 초기화.
    private static (double[][] W1, double[] b1, double[][] W2, double[] b2) InitWeights(int seed)
    {
        var rng = new Random(seed);
        int inDim = 4, hid = HiddenSize, outDim = 3;
        double lim1 = Math.Sqrt(6.0 / (inDim + hid));
        double lim2 = Math.Sqrt(6.0 / (hid + outDim));
        var W1 = new double[inDim][];
        for (int i = 0; i < inDim; i++)
        {
            W1[i] = new double[hid];
            for (int j = 0; j < hid; j++) W1[i][j] = (rng.NextDouble() * 2 - 1) * lim1;
        }
        var W2 = new double[hid][];
        for (int i = 0; i < hid; i++)
        {
            W2[i] = new double[outDim];
            for (int j = 0; j < outDim; j++) W2[i][j] = (rng.NextDouble() * 2 - 1) * lim2;
        }
        return (W1, new double[hid], W2, new double[outDim]);
    }
}
";

        /// <summary>
        /// "Mnist Demo" — MNIST 손글씨 숫자 (28×28, 10 classes) 분류. 사용자 스니펫이 HTTP URL 로 CSV 다운로드
        /// (%LOCALAPPDATA%/VSMVVM/Data/mnist 캐시). Iris 인프라 재사용 — Repeat + Delay + ChartViewNode.
        /// 5개 창: accuracy / loss / input_preview (Heatmap) / confusion / confusion_tpfn.
        /// </summary>
        [RelayCommand]
        private void LoadMnistDemo()
        {
            if (Container == null) { StatusMessage = "Mnist demo: container not available."; return; }

            var existing = Container.Snippets.FirstOrDefault(s => s.Category == "Mnist");
            if (existing != null)
            {
                existing.SourceCode = MnistDemoSource;
            }
            else
            {
                Container.Snippets.Add(new UserCodeSnippet("Mnist", MnistDemoSource));
            }
            Container.CompileAllSnippetsCommand.Execute(null);

            string[] requiredIds =
            {
                "Mnist.LearningRate", "Mnist.HiddenSize", "Mnist.Epochs",
                "Mnist.TrainSampleCap", "Mnist.ValSampleCap",
                "Mnist.LoadCsv", "Mnist.InitState", "Mnist.TrainOneEpoch", "Mnist.Evaluate",
            };
            foreach (var id in requiredIds)
            {
                if (NodeMetadataRegistry.Get(id) == null)
                {
                    StatusMessage = $"Mnist demo: compile OK but '{id}' not found.";
                    return;
                }
            }

            RebuildGraph(g =>
            {
                if (!g.Variables.ContainsKey("state"))
                    g.AddVariable("state", typeof(object), null);

                var start = g.AddNode(StartNode.TypeIdConst, 40, 40);

                // === 데이터 로드 (train/test 각각 CSV) ===
                var loadTr = g.AddNode("Mnist.LoadCsv", 40, 140);
                ((NodeBase)loadTr).SetLiteralInput("url", "https://pjreddie.com/media/files/mnist_train.csv");
                var trainCap = g.AddNode("Mnist.TrainSampleCap", 40, 240);
                g.Connect(trainCap.Id, "Out", loadTr.Id, "sampleCap");

                var loadVal = g.AddNode("Mnist.LoadCsv", 260, 140);
                ((NodeBase)loadVal).SetLiteralInput("url", "https://pjreddie.com/media/files/mnist_test.csv");
                var valCap = g.AddNode("Mnist.ValSampleCap", 260, 240);
                g.Connect(valCap.Id, "Out", loadVal.Id, "sampleCap");

                var initState = g.AddNode("Mnist.InitState", 480, 160);
                g.Connect(loadTr.Id,  "X", initState.Id, "Xtr");
                g.Connect(loadTr.Id,  "Y", initState.Id, "Ytr");
                g.Connect(loadVal.Id, "X", initState.Id, "Xval");
                g.Connect(loadVal.Id, "Y", initState.Id, "Yval");

                var setStateInit = (SetVariableNode)g.AddNode(SetVariableNode.TypeIdConst, 700, 160);
                setStateInit.ItemType = typeof(object);
                setStateInit.VariableName = "state";
                g.Connect(initState.Id, "Result", setStateInit.Id, "Value");

                // === Repeat 루프 ===
                var epochs = g.AddNode("Mnist.Epochs", 40, 340);
                var repeat = g.AddNode(RepeatNode.TypeIdConst, 940, 200);
                g.Connect(epochs.Id, "Out", repeat.Id, "Count");

                var getStateBody = (GetVariableNode)g.AddNode(GetVariableNode.TypeIdConst, 1160, 100);
                getStateBody.ItemType = typeof(object);
                getStateBody.VariableName = "state";

                var lr = g.AddNode("Mnist.LearningRate", 1160, 300);

                var train = g.AddNode("Mnist.TrainOneEpoch", 1380, 200);
                g.Connect(getStateBody.Id, "Value", train.Id, "stateObj");
                g.Connect(repeat.Id,       "Index", train.Id, "epoch");
                g.Connect(lr.Id,           "Out",   train.Id, "lr");

                var setStateBody = (SetVariableNode)g.AddNode(SetVariableNode.TypeIdConst, 1620, 200);
                setStateBody.ItemType = typeof(object);
                setStateBody.VariableName = "state";
                g.Connect(train.Id, "Result", setStateBody.Id, "Value");

                // 루프 안 ChartView 3개: accuracy / loss / input_preview(Heatmap).
                var chartAccLoop = (ChartViewNode)g.AddNode(ChartViewNode.TypeIdConst, 1840, 100);
                chartAccLoop.ViewName = "accuracy"; chartAccLoop.Kind = ChartKind.Line;
                var chartLossLoop = (ChartViewNode)g.AddNode(ChartViewNode.TypeIdConst, 1840, 200);
                chartLossLoop.ViewName = "loss"; chartLossLoop.Kind = ChartKind.Line;
                var chartInputLoop = (ChartViewNode)g.AddNode(ChartViewNode.TypeIdConst, 1840, 300);
                chartInputLoop.ViewName = "input_preview"; chartInputLoop.Kind = ChartKind.Heatmap;

                var delay = g.AddNode(DelayNode.TypeIdConst, 2060, 200);
                ((NodeBase)delay).SetLiteralInput("Seconds", 0.2);

                g.Connect(repeat.Id,         "Body", setStateBody.Id,   "In");
                g.Connect(setStateBody.Id,   "Then", chartAccLoop.Id,   "In");
                g.Connect(chartAccLoop.Id,   "Then", chartLossLoop.Id,  "In");
                g.Connect(chartLossLoop.Id,  "Then", chartInputLoop.Id, "In");
                g.Connect(chartInputLoop.Id, "Then", delay.Id,          "In");
                g.Connect(delay.Id,          "Then", repeat.Id,         "In");

                // Done: Evaluate + Output + confusion charts.
                var getStateEval = (GetVariableNode)g.AddNode(GetVariableNode.TypeIdConst, 940, 440);
                getStateEval.ItemType = typeof(object);
                getStateEval.VariableName = "state";

                var evalNode = g.AddNode("Mnist.Evaluate", 1160, 440);
                g.Connect(getStateEval.Id, "Value", evalNode.Id, "stateObj");

                var outTrainAcc = g.AddNode(OutputNode.TypeIdConst, 1380, 380);
                ((NodeBase)outTrainAcc).SetLiteralInput("Key", "train_accuracy");
                var outValAcc = g.AddNode(OutputNode.TypeIdConst, 1380, 450);
                ((NodeBase)outValAcc).SetLiteralInput("Key", "val_accuracy");
                var outConfMat = g.AddNode(OutputNode.TypeIdConst, 1380, 520);
                ((NodeBase)outConfMat).SetLiteralInput("Key", "confusion");
                var outPerClass = g.AddNode(OutputNode.TypeIdConst, 1380, 590);
                ((NodeBase)outPerClass).SetLiteralInput("Key", "per_class_acc");
                var outPerClassCounts = g.AddNode(OutputNode.TypeIdConst, 1380, 660);
                ((NodeBase)outPerClassCounts).SetLiteralInput("Key", "per_class_counts");

                g.Connect(evalNode.Id, "TrainAccuracy",    outTrainAcc.Id,       "Value");
                g.Connect(evalNode.Id, "ValAccuracy",      outValAcc.Id,         "Value");
                g.Connect(evalNode.Id, "Confusion",        outConfMat.Id,        "Value");
                g.Connect(evalNode.Id, "PerClassAccuracy", outPerClass.Id,       "Value");
                g.Connect(evalNode.Id, "PerClassCounts",   outPerClassCounts.Id, "Value");

                var chartConf = (ChartViewNode)g.AddNode(ChartViewNode.TypeIdConst, 1620, 540);
                chartConf.ViewName = "confusion"; chartConf.Kind = ChartKind.ConfusionMatrix;
                var chartTpFn = (ChartViewNode)g.AddNode(ChartViewNode.TypeIdConst, 1620, 620);
                chartTpFn.ViewName = "confusion_tpfn"; chartTpFn.Kind = ChartKind.ConfusionMatrix;

                var end = g.AddNode(EndNode.TypeIdConst, 1880, 580);

                g.Connect(start.Id,             "Then", setStateInit.Id,       "In");
                g.Connect(setStateInit.Id,      "Then", repeat.Id,             "In");
                g.Connect(repeat.Id,            "Done", outTrainAcc.Id,        "In");
                g.Connect(outTrainAcc.Id,       "Then", outValAcc.Id,          "In");
                g.Connect(outValAcc.Id,         "Then", outConfMat.Id,         "In");
                g.Connect(outConfMat.Id,        "Then", outPerClass.Id,        "In");
                g.Connect(outPerClass.Id,       "Then", outPerClassCounts.Id,  "In");
                g.Connect(outPerClassCounts.Id, "Then", chartConf.Id,          "In");
                g.Connect(chartConf.Id,         "Then", chartTpFn.Id,          "In");
                g.Connect(chartTpFn.Id,         "Then", end.Id,                "In");
            });
            StatusMessage = "Mnist demo loaded. Open ChartView windows, then Run — first run downloads ~15MB CSVs (cached in %LOCALAPPDATA%/VSMVVM/Data/mnist).";
        }

        /// <summary>
        /// MNIST 데모용 사용자 코드 조각 — HTTP 다운로드/캐시 + 784→128→10 MLP.
        /// pjreddie.com 미러 사용 (label,pixel0..pixel783 CSV 포맷).
        /// </summary>
        private const string MnistDemoSource = @"using System;
using System.Collections.Generic;
using System.Linq;
using VSMVVM.Core.Scheduler.Attributes;
using VSMVVM.WPF.Sample.Scheduler;

public sealed class MnistState
{
    public double[][] Xtr; public int[] Ytr;
    public double[][] Xval; public int[] Yval;
    public double[][] W1; public double[] b1;
    public double[][] W2; public double[] b2;
}

public static class MnistModel
{
    [ParameterNode(""Mnist.LearningRate"", Category = ""Mnist"", DisplayName = ""Learning Rate"")]
    public static double LearningRate = 0.01;

    [ParameterNode(""Mnist.HiddenSize"", Category = ""Mnist"", DisplayName = ""Hidden Size"")]
    public static int HiddenSize = 128;

    [ParameterNode(""Mnist.Epochs"", Category = ""Mnist"", DisplayName = ""Epochs"")]
    public static int Epochs = 10;

    [ParameterNode(""Mnist.TrainSampleCap"", Category = ""Mnist"", DisplayName = ""Train Sample Cap"")]
    public static int TrainSampleCap = 5000;

    [ParameterNode(""Mnist.ValSampleCap"", Category = ""Mnist"", DisplayName = ""Val Sample Cap"")]
    public static int ValSampleCap = 1000;

    // MNIST CSV 다운로드/캐시/파싱은 Sample 어셈블리의 MnistDataLoader 헬퍼로 위임 —
    // System.Net.Http 참조를 스니펫 컴파일 컨텍스트에서 걱정하지 않아도 됨.
    [MethodNode(""Mnist.LoadCsv"", Category = ""Mnist"", DisplayName = ""Load CSV"")]
    public static (double[][] X, int[] Y) LoadCsv(string url, int sampleCap)
        => MnistDataLoader.LoadCsv(url, sampleCap);

    [MethodNode(""Mnist.InitState"", Category = ""Mnist"", DisplayName = ""Init State"")]
    public static object InitState(double[][] Xtr, int[] Ytr, double[][] Xval, int[] Yval)
    {
        var (W1, b1, W2, b2) = InitWeights(42);
        return new MnistState
        {
            Xtr = Xtr, Ytr = Ytr,
            Xval = Xval, Yval = Yval,
            W1 = W1, b1 = b1, W2 = W2, b2 = b2,
        };
    }

    // 한 에폭 학습. 매 에폭마다 ChartLog.Push 로 실시간 스트리밍.
    // 첫 학습 샘플의 28×28 을 input_preview 히트맵으로 push (매 에폭 갱신).
    [MethodNode(""Mnist.TrainOneEpoch"", Category = ""Mnist"", DisplayName = ""Train One Epoch"")]
    public static object TrainOneEpoch(object stateObj, int epoch, double lr)
    {
        var state = (MnistState)stateObj;
        int inDim = 784, hid = HiddenSize, outDim = 10;
        var Xn = state.Xtr; var Y = state.Ytr;
        var W1 = state.W1; var b1 = state.b1;
        var W2 = state.W2; var b2 = state.b2;
        int n = Xn.Length;

        var rng = new Random(epoch * 7919 + 13);
        var idx = Enumerable.Range(0, n).ToArray();
        for (int i = n - 1; i > 0; i--) { int j = rng.Next(i + 1); (idx[i], idx[j]) = (idx[j], idx[i]); }

        double totalLoss = 0;
        for (int step = 0; step < n; step++)
        {
            int k = idx[step];
            var x = Xn[k];
            int y = Y[k];

            var h = new double[hid];
            for (int j = 0; j < hid; j++)
            {
                double s = b1[j];
                for (int i = 0; i < inDim; i++) s += x[i] * W1[i][j];
                h[j] = Math.Tanh(s);
            }
            var logits = new double[outDim];
            for (int j = 0; j < outDim; j++)
            {
                double s = b2[j];
                for (int i = 0; i < hid; i++) s += h[i] * W2[i][j];
                logits[j] = s;
            }
            double m = logits.Max();
            var probs = new double[outDim];
            double sumExp = 0;
            for (int j = 0; j < outDim; j++) { probs[j] = Math.Exp(logits[j] - m); sumExp += probs[j]; }
            for (int j = 0; j < outDim; j++) probs[j] /= sumExp;
            totalLoss += -Math.Log(Math.Max(probs[y], 1e-12));

            var dlogits = new double[outDim];
            for (int j = 0; j < outDim; j++) dlogits[j] = probs[j] - (j == y ? 1.0 : 0.0);
            var dh = new double[hid];
            for (int i = 0; i < hid; i++)
            {
                double s = 0;
                for (int j = 0; j < outDim; j++) s += dlogits[j] * W2[i][j];
                dh[i] = s * (1 - h[i] * h[i]);
            }
            for (int i = 0; i < hid; i++)
                for (int j = 0; j < outDim; j++)
                    W2[i][j] -= lr * dlogits[j] * h[i];
            for (int j = 0; j < outDim; j++) b2[j] -= lr * dlogits[j];
            for (int i = 0; i < inDim; i++)
                for (int j = 0; j < hid; j++)
                    W1[i][j] -= lr * dh[j] * x[i];
            for (int j = 0; j < hid; j++) b1[j] -= lr * dh[j];
        }
        double epochLoss = totalLoss / n;

        double trAcc = AccuracyOn(state, state.Xtr, state.Ytr);
        double vaAcc = AccuracyOn(state, state.Xval, state.Yval);

        ChartLog.Push(""accuracy"", epoch, ""trainAcc"", trAcc);
        ChartLog.Push(""accuracy"", epoch, ""valAcc"", vaAcc);
        ChartLog.Push(""loss"", epoch, ""loss"", epochLoss);

        // 매 에폭 시연용으로 첫 학습 샘플의 28×28 을 히트맵으로 push.
        var preview = new double[28, 28];
        var first = state.Xtr[idx[0]];
        for (int r = 0; r < 28; r++)
            for (int c = 0; c < 28; c++)
                preview[r, c] = first[r * 28 + c];
        ChartLog.PushMatrix(""input_preview"", preview,
            new string[0], new string[0], ""row"", ""col"");

        return state;
    }

    [MethodNode(""Mnist.Evaluate"", Category = ""Mnist"", DisplayName = ""Evaluate"")]
    public static (double TrainAccuracy, double ValAccuracy,
                   int[][] Confusion, double[] PerClassAccuracy, int[][] PerClassCounts)
        Evaluate(object stateObj)
    {
        var state = (MnistState)stateObj;
        int outDim = 10;
        var classNames = Enumerable.Range(0, 10).Select(i => i.ToString()).ToArray();
        var tpFnLabels = new[] { ""TP"", ""TN"", ""FP"", ""FN"" };

        var trainPreds = Predict(state, state.Xtr);
        var valPreds = Predict(state, state.Xval);
        double trainAcc = AccuracyFromPreds(trainPreds, state.Ytr);
        double valAcc = AccuracyFromPreds(valPreds, state.Yval);

        var conf = new int[outDim][];
        for (int i = 0; i < outDim; i++) conf[i] = new int[outDim];
        for (int i = 0; i < state.Yval.Length; i++) conf[state.Yval[i]][valPreds[i]]++;

        var perClass = new double[outDim];
        for (int i = 0; i < outDim; i++)
        {
            int rowSum = 0; for (int j = 0; j < outDim; j++) rowSum += conf[i][j];
            perClass[i] = rowSum == 0 ? 0 : conf[i][i] / (double)rowSum;
        }

        int total = state.Yval.Length;
        var perClassCounts = new int[outDim][];
        for (int c = 0; c < outDim; c++)
        {
            int tp = conf[c][c];
            int fn = 0; for (int j = 0; j < outDim; j++) fn += conf[c][j]; fn -= tp;
            int fp = 0; for (int i = 0; i < outDim; i++) fp += conf[i][c]; fp -= tp;
            int tn = total - tp - fn - fp;
            perClassCounts[c] = new[] { tp, tn, fp, fn };
        }

        ChartLog.PushMatrix(""confusion"", conf, classNames, classNames, ""Actual"", ""Predicted"");
        ChartLog.PushMatrix(""confusion_tpfn"", perClassCounts, classNames, tpFnLabels, ""Class"", ""Metric"");

        return (trainAcc, valAcc, conf, perClass, perClassCounts);
    }

    private static int[] Predict(MnistState s, double[][] Xn)
    {
        int hid = HiddenSize, inDim = 784, outDim = 10;
        var preds = new int[Xn.Length];
        for (int k = 0; k < Xn.Length; k++)
        {
            var x = Xn[k];
            var hv = new double[hid];
            for (int j = 0; j < hid; j++)
            {
                double sv = s.b1[j];
                for (int i = 0; i < inDim; i++) sv += x[i] * s.W1[i][j];
                hv[j] = Math.Tanh(sv);
            }
            double bestLogit = double.NegativeInfinity; int bestJ = 0;
            for (int j = 0; j < outDim; j++)
            {
                double sv = s.b2[j];
                for (int i = 0; i < hid; i++) sv += hv[i] * s.W2[i][j];
                if (sv > bestLogit) { bestLogit = sv; bestJ = j; }
            }
            preds[k] = bestJ;
        }
        return preds;
    }

    private static double AccuracyOn(MnistState s, double[][] X, int[] Y)
        => AccuracyFromPreds(Predict(s, X), Y);

    private static double AccuracyFromPreds(int[] preds, int[] labels)
    {
        int correct = 0;
        for (int i = 0; i < preds.Length; i++) if (preds[i] == labels[i]) correct++;
        return correct / (double)preds.Length;
    }

    private static (double[][] W1, double[] b1, double[][] W2, double[] b2) InitWeights(int seed)
    {
        var rng = new Random(seed);
        int inDim = 784, hid = HiddenSize, outDim = 10;
        double lim1 = Math.Sqrt(6.0 / (inDim + hid));
        double lim2 = Math.Sqrt(6.0 / (hid + outDim));
        var W1 = new double[inDim][];
        for (int i = 0; i < inDim; i++)
        {
            W1[i] = new double[hid];
            for (int j = 0; j < hid; j++) W1[i][j] = (rng.NextDouble() * 2 - 1) * lim1;
        }
        var W2 = new double[hid][];
        for (int i = 0; i < hid; i++)
        {
            W2[i] = new double[outDim];
            for (int j = 0; j < outDim; j++) W2[i][j] = (rng.NextDouble() * 2 - 1) * lim2;
        }
        return (W1, new double[hid], W2, new double[outDim]);
    }
}
";

        /// <summary>
        /// "OpenCV Demo" — OpenCvSharp 노드 6종을 사용자 코드 조각으로 추가한 뒤
        /// 파일 IO + ImageView 모니터 + Canny + ImWrite 의 시연 그래프를 자동 구성.
        /// </summary>
        [RelayCommand]
        private void LoadOpenCvDemo()
        {
            if (Container == null) { StatusMessage = "OpenCV demo: container not available."; return; }

            // 1) OpenCV 조각을 컨테이너 Snippets 에 추가/교체 후 일괄 컴파일 → 등록.
            var existing = Container.Snippets.FirstOrDefault(s => s.Category == "OpenCV");
            if (existing != null)
            {
                existing.SourceCode = OpenCvDemoSource;
            }
            else
            {
                Container.Snippets.Add(new UserCodeSnippet("OpenCV", OpenCvDemoSource));
            }
            Container.CompileAllSnippetsCommand.Execute(null);

            // 2) 등록 확인 — 6개 typeId 가 모두 보여야 함.
            string[] requiredIds =
            {
                "Demo.OpenCv.ImRead", "Demo.OpenCv.ImWrite", "Demo.OpenCv.Canny",
                "Demo.OpenCv.Threshold", "Demo.OpenCv.CountNonZero", "Demo.OpenCv.CreateGradient",
            };
            foreach (var id in requiredIds)
            {
                if (NodeMetadataRegistry.Get(id) == null)
                {
                    StatusMessage = $"OpenCV demo: compile OK but [MethodNode(\"{id}\")] not found.";
                    return;
                }
            }

            // 3) 그래프 재구성. Delay 노드는 모두 제거 — exec 흐름이 막힘 없이 끝까지 도달.
            //    구조 (좌→우):
            //      Start → ImRead → Set'src' → ImageView'src' → Canny → Set'edges' → ImageView'edges' → ImWrite
            //              → CountNonZero → Output('edgePixels') → Output('saved') → End
            //    data: Get'src'/'edges' 가 각 단계에서 변수 값을 끌어옴.
            var assetIn = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "app.png");
            var assetOut = System.IO.Path.Combine(System.AppContext.BaseDirectory, "app_result.png");
            RebuildGraph(g =>
            {
                g.AddVariable("src", typeof(Mat), null);
                g.AddVariable("edges", typeof(Mat), null);

                var start = g.AddNode(StartNode.TypeIdConst, 60, 240);

                var read = g.AddNode("Demo.OpenCv.ImRead", 240, 240);
                ((NodeBase)read).SetLiteralInput("path", assetIn);

                SetVariableNode NewSet(double x, double y, string varName)
                {
                    var n = (SetVariableNode)g.AddNode(SetVariableNode.TypeIdConst, x, y);
                    n.ItemType = typeof(Mat);
                    n.VariableName = varName;
                    return n;
                }
                GetVariableNode NewGet(double x, double y, string varName)
                {
                    var n = (GetVariableNode)g.AddNode(GetVariableNode.TypeIdConst, x, y);
                    n.ItemType = typeof(Mat);
                    n.VariableName = varName;
                    return n;
                }

                var setSrc = NewSet(460, 240, "src");
                var getSrc1 = NewGet(680, 120, "src");

                var viewSrc = (ImageViewNode)g.AddNode(ImageViewNode.TypeIdConst, 680, 240);
                viewSrc.ViewName = "src";

                var getSrc2 = NewGet(900, 120, "src");

                var canny = g.AddNode("Demo.OpenCv.Canny", 900, 240);
                ((NodeBase)canny).SetLiteralInput("threshold1", 50.0);
                ((NodeBase)canny).SetLiteralInput("threshold2", 150.0);

                var setEdges = NewSet(1120, 240, "edges");
                var getEdges1 = NewGet(1340, 120, "edges");

                var viewEdges = (ImageViewNode)g.AddNode(ImageViewNode.TypeIdConst, 1340, 240);
                viewEdges.ViewName = "edges";

                var getEdges2 = NewGet(1560, 120, "edges");

                var write = g.AddNode("Demo.OpenCv.ImWrite", 1560, 240);
                ((NodeBase)write).SetLiteralInput("path", assetOut);

                var getEdges3 = NewGet(1780, 120, "edges");

                var count = g.AddNode("Demo.OpenCv.CountNonZero", 1780, 240);
                var outCount = g.AddNode(OutputNode.TypeIdConst, 2000, 180);
                ((NodeBase)outCount).SetLiteralInput("Key", "edgePixels");
                var outSaved = g.AddNode(OutputNode.TypeIdConst, 2000, 300);
                ((NodeBase)outSaved).SetLiteralInput("Key", "saved");
                var end = g.AddNode(EndNode.TypeIdConst, 2220, 240);

                // exec 흐름 — Delay 없이 직접 연결.
                g.Connect(start.Id, "Then", read.Id, "In");
                g.Connect(read.Id, "Then", setSrc.Id, "In");
                g.Connect(setSrc.Id, "Then", viewSrc.Id, "In");
                g.Connect(viewSrc.Id, "Then", canny.Id, "In");
                g.Connect(canny.Id, "Then", setEdges.Id, "In");
                g.Connect(setEdges.Id, "Then", viewEdges.Id, "In");
                g.Connect(viewEdges.Id, "Then", write.Id, "In");
                g.Connect(write.Id, "Then", count.Id, "In");
                g.Connect(count.Id, "Then", outCount.Id, "In");
                g.Connect(outCount.Id, "Then", outSaved.Id, "In");
                g.Connect(outSaved.Id, "Then", end.Id, "In");

                // data 흐름
                g.Connect(read.Id, "Result", setSrc.Id, "Value");
                g.Connect(getSrc1.Id, "Value", viewSrc.Id, "Image");
                g.Connect(getSrc2.Id, "Value", canny.Id, "image");
                g.Connect(canny.Id, "Result", setEdges.Id, "Value");
                g.Connect(getEdges1.Id, "Value", viewEdges.Id, "Image");
                g.Connect(getEdges2.Id, "Value", write.Id, "image");
                g.Connect(write.Id, "Result", outSaved.Id, "Value");
                g.Connect(getEdges3.Id, "Value", count.Id, "image");
                g.Connect(count.Id, "Result", outCount.Id, "Value");
            });

            StatusMessage = "OpenCV demo loaded: ImRead → Set'src' → View'src' → Canny → Set'edges' → View'edges' → ImWrite → CountNonZero → Output → End. Press Run.";
        }

        /// <summary>
        /// View 가 NodeView.NodeDoubleClicked 라우티드 이벤트를 받아 호출. ImageViewNode 면 그 ViewName 으로
        /// 모덜리스 윈도우를 띄움 (이미 있으면 포커스만). Sample 한정.
        /// </summary>
        public void OpenImageViewForNode(object node)
        {
            if (node is not ImageViewNode iv) return;
            var viewName = iv.ViewName ?? "view";
            if (_imageViewWindows.TryGetValue(viewName, out var existing) && existing.IsLoaded)
            {
                existing.Activate();
                return;
            }
            try
            {
                var sp = ServiceLocator.GetServiceProvider();
                var win = sp.GetService(typeof(Views.ImageViewWindow).Name) as System.Windows.Window;
                if (win == null) return;
                if (win is System.Windows.FrameworkElement fe && fe.DataContext is ImageViewWindowViewModel vm)
                {
                    vm.DialogParameter = (viewName, SnapshotStore);
                    vm.RequestClose += (s, e) => win.Close();
                }
                win.Closed += (s, e) => _imageViewWindows.Remove(viewName);
                win.Owner = System.Windows.Application.Current?.MainWindow;
                _imageViewWindows[viewName] = win;
                win.Show();
            }
            catch { /* 다이얼로그 열기 실패는 무시 */ }
        }

        /// <summary>
        /// View 가 NodeView.NodeDoubleClicked 라우티드 이벤트를 받아 호출. ChartViewNode 면 그 ViewName+Kind 으로
        /// 모덜리스 윈도우를 띄움 (이미 있으면 포커스만). Sample 한정.
        /// </summary>
        public void OpenChartViewForNode(object node)
        {
            if (node is not ChartViewNode cv) return;
            var viewName = cv.ViewName ?? "chart";
            if (_chartViewWindows.TryGetValue(viewName, out var existing) && existing.IsLoaded)
            {
                existing.Activate();
                return;
            }
            try
            {
                var sp = ServiceLocator.GetServiceProvider();
                var win = sp.GetService(typeof(Views.ChartViewWindow).Name) as System.Windows.Window;
                if (win == null) return;
                if (win is System.Windows.FrameworkElement fe && fe.DataContext is ChartViewWindowViewModel vm)
                {
                    vm.DialogParameter = (viewName, cv.Kind, ChartStore);
                    vm.RequestClose += (s, e) => win.Close();
                }
                win.Closed += (s, e) => _chartViewWindows.Remove(viewName);
                win.Owner = System.Windows.Application.Current?.MainWindow;
                _chartViewWindows[viewName] = win;
                win.Show();
            }
            catch { /* 다이얼로그 열기 실패는 무시 */ }
        }

        // ============= OpenCV 데모 사용자 코드 (원본 그대로) =============

        private const string OpenCvDemoSource = @"// OpenCvSharp 데모 — 각 연산을 별도 [MethodNode] 로 노출하여 그래프에서 조립 가능하게.
using OpenCvSharp;
using VSMVVM.Core.Scheduler.Attributes;

namespace Demo.OpenCv
{
    public static class CvOps
    {
        [MethodNode(""Demo.OpenCv.ImRead"", Category = ""OpenCV"")]
        public static Mat ImRead(string path)
        {
            if (string.IsNullOrEmpty(path)) return new Mat();
            return Cv2.ImRead(path, ImreadModes.Grayscale);
        }

        [MethodNode(""Demo.OpenCv.ImWrite"", Category = ""OpenCV"")]
        public static bool ImWrite(string path, Mat image)
        {
            if (string.IsNullOrEmpty(path) || image == null || image.Empty()) return false;
            return Cv2.ImWrite(path, image);
        }

        [MethodNode(""Demo.OpenCv.Canny"", Category = ""OpenCV"")]
        public static Mat Canny(Mat image, double threshold1, double threshold2)
        {
            var edges = new Mat();
            if (image == null || image.Empty()) return edges;
            Cv2.Canny(image, edges, threshold1, threshold2);
            return edges;
        }

        [MethodNode(""Demo.OpenCv.Threshold"", Category = ""OpenCV"")]
        public static Mat Threshold(Mat image, double thresh, double maxVal)
        {
            var bin = new Mat();
            if (image == null || image.Empty()) return bin;
            Cv2.Threshold(image, bin, thresh, maxVal, ThresholdTypes.Binary);
            return bin;
        }

        [MethodNode(""Demo.OpenCv.CountNonZero"", Category = ""OpenCV"")]
        public static int CountNonZero(Mat image)
        {
            if (image == null || image.Empty()) return 0;
            return Cv2.CountNonZero(image);
        }

        [MethodNode(""Demo.OpenCv.CreateGradient"", Category = ""OpenCV"")]
        public static Mat CreateGradient(int size)
        {
            if (size <= 0) size = 256;
            var mat = new Mat(size, size, MatType.CV_8UC1);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    mat.Set<byte>(y, x, (byte)(x * 255 / (size - 1)));
            return mat;
        }
    }
}
";

        // ============= Log Flood 부하 데모 =============

        /// <summary>Log Flood 중복 실행 방지 — CTS 하나만 유지.</summary>
        private System.Threading.CancellationTokenSource _logFloodCts;

        /// <summary>
        /// "Log Flood (Stress)" — 백그라운드에서 <see cref="ISchedulerLogSink.Write"/> 와
        /// <see cref="IExecutionHistoryStore.Add"/> 를 동시에 폭주시켜 SchedulerLogViewModel /
        /// ExecutionHistoryViewModel 두 VM 의 배치 flush 를 함께 검증.
        ///
        /// 안전 장치:
        ///   - CancellationTokenSource(TimeSpan) 로 5초 후 자동 취소. 앱 죽어도 워커 확실 종료.
        ///   - 로그 4워커 × Sleep(1ms) = ~4 kHz. 히스토리 1워커 × Sleep(5ms) = ~200 Hz.
        ///   - 그래프는 빈 Start→End 로 교체 (UX 일치).
        /// </summary>
        private void LoadLogFloodDemo()
        {
            var sink = LogSink;
            var store = HistoryStore;
            if (sink == null || store == null)
            {
                StatusMessage = "LogSink / HistoryStore not available — cannot run log flood.";
                return;
            }

            RebuildGraph(g =>
            {
                var start = g.AddNode(StartNode.TypeIdConst, 200, 200);
                var end = g.AddNode(EndNode.TypeIdConst, 600, 200);
                g.Connect(start.Id, "Then", end.Id, "In");
            });

            _logFloodCts?.Cancel();
            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            _logFloodCts = cts;

            var floodRunId = Guid.NewGuid();
            var graphId = Model?.Id ?? Guid.Empty;
            StatusMessage = "Log Flood 시작 — 5초간 로그 4워커 + 히스토리 1워커.";

            // 로그 워커.
            for (int worker = 0; worker < 4; worker++)
            {
                int w = worker;
                System.Threading.Tasks.Task.Run(() =>
                {
                    int i = 0;
                    var token = cts.Token;
                    while (!token.IsCancellationRequested)
                    {
                        sink.Write(new SchedulerLogEntry(
                            DateTimeOffset.Now, SchedulerLogLevel.Info,
                            floodRunId, null, string.Empty,
                            $"flood w{w} #{i++}", null));
                        System.Threading.Thread.Sleep(1);
                    }
                }, cts.Token);
            }

            // 히스토리 워커 — 매 20ms 마다 fake ExecutionRun push.
            // Records 3개 + MarkCompleted 로 Gantt 가 실제로 그릴 수 있는 최소 run 을 구성.
            var fakeNodeAId = Guid.NewGuid();
            var fakeNodeBId = Guid.NewGuid();
            var fakeNodeCId = Guid.NewGuid();
            System.Threading.Tasks.Task.Run(() =>
            {
                var token = cts.Token;
                while (!token.IsCancellationRequested)
                {
                    var startedAt = DateTimeOffset.Now;
                    var run = new ExecutionRun(Guid.NewGuid(), graphId, startedAt);
                    run.AppendRecord(new NodeExecutionRecord(
                        fakeNodeAId, StartNode.TypeIdConst, startedAt,
                        TimeSpan.FromMilliseconds(2), null, null, null));
                    run.AppendRecord(new NodeExecutionRecord(
                        fakeNodeBId, "Flood.Fake", startedAt.AddMilliseconds(3),
                        TimeSpan.FromMilliseconds(8), null, null, null));
                    run.AppendRecord(new NodeExecutionRecord(
                        fakeNodeCId, EndNode.TypeIdConst, startedAt.AddMilliseconds(12),
                        TimeSpan.FromMilliseconds(1), null, null, null));
                    run.MarkCompleted(ExecutionStatus.Completed, startedAt.AddMilliseconds(15));
                    store.Add(run);
                    System.Threading.Thread.Sleep(20);
                }
            }, cts.Token);

            System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
            {
                var app = System.Windows.Application.Current;
                app?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (cts != _logFloodCts) return;
                    StatusMessage = $"Log Flood 종료. LogVm.Entries={LogVm.Entries.Count} · HistoryVm.Runs={HistoryVm.Runs.Count}";
                }));
            });
        }
    }
}
