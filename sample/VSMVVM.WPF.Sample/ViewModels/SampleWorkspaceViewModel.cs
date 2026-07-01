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

        /// <summary>현재 열려있는 ImageViewWindow — ViewName 별 1개. 닫히면 제거.</summary>
        private readonly Dictionary<string, System.Windows.Window> _imageViewWindows = new();

        /// <summary>Sample 만의 변수 타입 옵션 — 기본 + OpenCV Mat.</summary>
        public override IReadOnlyList<string> VariableTypeOptions { get; } =
            new[] { "int", "double", "string", "bool", "long", "Mat" };

        /// <summary>
        /// Phase M — 컨테이너 (사용자 코드 전역 보유) 의 reference. 데모 명령이 컨테이너 Snippets 에 주입 + 컴파일.
        /// 테스트 시나리오에서 컨테이너 없이 워크스페이스만 만들 수 있도록 nullable.
        /// </summary>
        public SchedulerDemoViewModel Container { get; }

        public SampleWorkspaceViewModel(
            ICompilationService compiler,
            INodePaletteService palette,
            ISchedulerService scheduler = null,
            IMessenger messenger = null,
            IUndoRedoService undo = null,
            ILoggerService logger = null,
            IFileDialogService fileDialog = null,
            IWindowService windowService = null,
            SchedulerDemoViewModel container = null,
            string displayName = "Workspace")
            : base(compiler, palette, scheduler, messenger, undo, logger, fileDialog, windowService, displayName)
        {
            Container = container;
            // Run 시작 직전: SnapshotStore 클리어 후 ctx.Variables 에 주입 → ImageViewNode 가 접근 가능.
            ConfigureContext = ctx =>
            {
                SnapshotStore.Clear();
                ctx.Variables[ImageViewNode.SnapshotStoreKey] = SnapshotStore;
            };
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

            const string numsTypeId = "Demo.Params.Nums";
            if (NodeMetadataRegistry.Get(numsTypeId) == null)
            {
                StatusMessage = $"Collection demo: compile OK but [ParameterNode(\"{numsTypeId}\")] not found.";
                return;
            }

            // 2) 그래프 빌드 — Start → CustomParameter("Demo.Params.Nums") → SetVariable("nums") → GetVariable → Output("nums") → End.
            RebuildGraph(g =>
            {
                if (!g.Variables.ContainsKey("nums"))
                    g.AddVariable("nums", typeof(List<double>), new List<double>());

                var start = g.AddNode(StartNode.TypeIdConst, 80, 200);
                var nums = g.AddNode(numsTypeId, 80, 360);

                var setVar = (SetVariableNode)g.AddNode(SetVariableNode.TypeIdConst, 360, 200);
                setVar.ItemType = typeof(List<double>);
                setVar.VariableName = "nums";

                var getVar = (GetVariableNode)g.AddNode(GetVariableNode.TypeIdConst, 600, 100);
                getVar.ItemType = typeof(List<double>);
                getVar.VariableName = "nums";

                var output = g.AddNode(OutputNode.TypeIdConst, 600, 240);
                ((NodeBase)output).SetLiteralInput("Key", "nums");
                var end = g.AddNode(EndNode.TypeIdConst, 840, 240);

                g.Connect(start.Id, "Then", setVar.Id, "In");
                g.Connect(nums.Id, "Out", setVar.Id, "Value");
                g.Connect(setVar.Id, "Then", output.Id, "In");
                g.Connect(getVar.Id, "Value", output.Id, "Value");
                g.Connect(output.Id, "Then", end.Id, "In");
            });
            StatusMessage = "Collection demo loaded: ParameterNode(\"Demo.Params.Nums\") → Set 'nums' → Output. Press Run.";
        }

        /// <summary>
        /// Phase L Collection 데모용 코드 조각 — [ParameterNode] 정적 필드 시연.
        /// 사용자가 에디터에서 자유 편집 가능.
        /// </summary>
        private const string CollectionDemoSource = @"using System.Collections.Generic;
using VSMVVM.Core.Scheduler.Attributes;

public static class CollectionDemoParams
{
    [ParameterNode(""Demo.Params.Nums"", Category = ""Parameters"", DisplayName = ""Nums"")]
    public static List<double> Nums = new() { 1.0, 2.5, 3.5, 4.0 };
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
                "Demo.Player.Name", "Demo.Player.Score", "Demo.Player.IsWinner", "Demo.Player.FormatReport",
            };
            foreach (var id in requiredIds)
            {
                if (NodeMetadataRegistry.Get(id) == null)
                {
                    StatusMessage = $"Parameter demo: compile OK but '{id}' not found.";
                    return;
                }
            }

            // 2) 그래프 빌드 — Start → [Name + Score + IsWinner] → FormatReport → Output("report") → End.
            RebuildGraph(g =>
            {
                var start = g.AddNode(StartNode.TypeIdConst, 80, 240);
                var paramName = g.AddNode("Demo.Player.Name", 80, 100);
                var paramScore = g.AddNode("Demo.Player.Score", 80, 200);
                var paramWinner = g.AddNode("Demo.Player.IsWinner", 80, 300);

                var format = g.AddNode("Demo.Player.FormatReport", 360, 200);
                var output = g.AddNode(OutputNode.TypeIdConst, 640, 200);
                ((NodeBase)output).SetLiteralInput("Key", "report");
                var end = g.AddNode(EndNode.TypeIdConst, 880, 200);

                g.Connect(start.Id, "Then", format.Id, "In");
                g.Connect(paramName.Id, "Out", format.Id, "name");
                g.Connect(paramScore.Id, "Out", format.Id, "score");
                g.Connect(paramWinner.Id, "Out", format.Id, "isWinner");
                g.Connect(format.Id, "Result", output.Id, "Value");
                g.Connect(format.Id, "Then", output.Id, "In");
                g.Connect(output.Id, "Then", end.Id, "In");
            });
            StatusMessage = "Parameter demo loaded: 3 ParameterNodes + 1 MethodNode → Output('report'). Edit fields in code editor and re-run.";
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
    }
}
