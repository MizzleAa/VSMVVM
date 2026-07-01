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
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.WPF.Sample.Scheduler;
using VSMVVM.WPF.Scheduler.Editor.Themes;
using VSMVVM.WPF.Scheduler.Services;
using VSMVVM.WPF.Scheduler.ViewModels;
using VSMVVM.WPF.Services;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// 멀티 탭 워크스페이스 컨테이너 — 화면 최상위 ViewModel.
    /// <para>
    /// 사용자 결정 정책:
    ///   • 한 탭 = 한 <see cref="SampleWorkspaceViewModel"/> (독립 그래프 + 코드 조각 + 로그/히스토리).
    ///   • 새 워크스페이스는 빈 그래프로 시작.
    ///   • 마지막 탭 닫기 허용 (VS Code 패턴) → Workspaces 비어있을 수 있음.
    ///   • <see cref="RunAllWorkspacesCommand"/> / <see cref="StopAllWorkspacesCommand"/> 가 모든 워크스페이스에 동작.
    ///     각 워크스페이스의 RunCommand 는 AsyncRelayCommand 라 자동 병렬 실행.
    /// </para>
    /// <para>
    /// 글로벌 서비스(컴파일러/스케줄러/메신저/팔레트/로거/윈도우/파일다이얼로그) 는 모든 탭이 공유.
    /// </para>
    /// </summary>
    public partial class SchedulerDemoViewModel : ViewModelBase
    {
        private readonly ISchedulerService _scheduler;
        private readonly IMessenger _messenger;
        private readonly ICompilationService _compiler;
        private readonly IUndoRedoService _undo;
        private readonly IFileDialogService _fileDialog;
        private readonly INodePaletteService _palette;
        private readonly ILoggerService _logger;
        private readonly IWindowService _windowService;

        /// <summary>모든 워크스페이스(탭) — 0개 가능 (마지막 탭 닫기 허용).</summary>
        public ObservableCollection<SampleWorkspaceViewModel> Workspaces { get; } = new();

        /// <summary>현재 활성 탭. Workspaces 가 비어있으면 null.</summary>
        [Property] private SampleWorkspaceViewModel _activeWorkspace;

        /// <summary>전역 에디터 테마 — 사용자 코드 편집기 다이얼로그 등에 사용.</summary>
        [Property] private EditorTheme _editorTheme = EditorTheme.Dark;

        /// <summary>
        /// Phase M — 전역 사용자 코드 조각. NodeMetadataRegistry 가 프로세스 전역이라 워크스페이스 단위로
        /// 두면 탭간 typeId 충돌. 모든 탭이 같은 Snippets 컬렉션을 공유.
        /// </summary>
        public ObservableCollection<UserCodeSnippet> Snippets { get; } = new();

        /// <summary>컨테이너 레벨 상태 메시지 — CompileAll / SaveCodeJson / LoadCodeJson 결과 표시.</summary>
        [Property] private string _containerStatusMessage = string.Empty;

        public SchedulerDemoViewModel(
            ISchedulerService scheduler,
            IMessenger messenger,
            ICompilationService compiler,
            IUndoRedoService undo,
            IFileDialogService fileDialog,
            INodePaletteService palette,
            ILoggerService logger,
            IWindowService windowService = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            _undo = undo ?? throw new ArgumentNullException(nameof(undo));
            _fileDialog = fileDialog ?? throw new ArgumentNullException(nameof(fileDialog));
            _palette = palette ?? throw new ArgumentNullException(nameof(palette));
            _logger = logger;
            _windowService = windowService;

            BuiltInNodes.EnsureRegistered();
            RegisterSampleNodes();
            // Phase K — Variable Get/Set 노드는 단일 등록이라 타입별 사전 등록 불필요.
            // 인스턴스의 ItemType 속성으로 그래프 변수의 CLR 타입과 매칭.

            // 초기 탭 1개로 시작 — 빈 그래프 (사용자 결정).
            var first = CreateWorkspace();
            Workspaces.Add(first);
            ActiveWorkspace = first;
        }

        private SampleWorkspaceViewModel CreateWorkspace()
        {
            int n = Workspaces.Count + 1;
            return new SampleWorkspaceViewModel(
                compiler: _compiler,
                palette: _palette,
                scheduler: _scheduler,
                messenger: _messenger,
                undo: _undo,
                logger: _logger,
                fileDialog: _fileDialog,
                windowService: _windowService,
                container: this,
                displayName: $"Workspace {n}");
        }

        // ============= 컨테이너 명령 =============

        /// <summary>새 탭 추가 — 빈 워크스페이스 생성 후 active 로 전환.</summary>
        [RelayCommand]
        private void NewWorkspace()
        {
            var ws = CreateWorkspace();
            Workspaces.Add(ws);
            ActiveWorkspace = ws;
        }

        /// <summary>탭 닫기 — 활성 탭이 닫히면 인접 탭이 새 active. 마지막 탭 닫으면 빈 상태(VS Code 패턴).</summary>
        [RelayCommand]
        private void CloseWorkspace(SampleWorkspaceViewModel target)
        {
            if (target == null) return;
            var idx = Workspaces.IndexOf(target);
            if (idx < 0) return;

            bool wasActive = (ActiveWorkspace == target);
            Workspaces.RemoveAt(idx);

            if (wasActive)
            {
                if (Workspaces.Count == 0)
                {
                    ActiveWorkspace = null;
                }
                else
                {
                    var nextIdx = Math.Min(idx, Workspaces.Count - 1);
                    ActiveWorkspace = Workspaces[nextIdx];
                }
            }
        }

        /// <summary>모든 워크스페이스를 동시에 실행 — 각 GraphVm.RunCommand 호출 (AsyncRelayCommand 라 병렬).</summary>
        [RelayCommand]
        private void RunAllWorkspaces()
        {
            foreach (var ws in Workspaces)
            {
                var cmd = ws.GraphVm?.RunCommand;
                if (cmd != null && cmd.CanExecute(null))
                {
                    cmd.Execute(null);
                }
            }
        }

        /// <summary>모든 실행 중 워크스페이스 중지.</summary>
        [RelayCommand]
        private void StopAllWorkspaces()
        {
            foreach (var ws in Workspaces)
            {
                var cmd = ws.GraphVm?.StopCommand;
                if (cmd != null && cmd.CanExecute(null))
                {
                    cmd.Execute(null);
                }
            }
        }

        /// <summary>전역 에디터 테마 토글 — 모든 코드 다이얼로그에 영향.</summary>
        [RelayCommand]
        private void ToggleTheme()
        {
            EditorTheme = EditorTheme == EditorTheme.Dark ? EditorTheme.Light : EditorTheme.Dark;
        }

        // ============= 사용자 코드 (전역) =============

        /// <summary>
        /// 모든 Snippets 를 일괄 컴파일 → CustomNodeFactory 로 등록 → 모든 워크스페이스 팔레트 갱신.
        /// 재컴파일 시 이전 RegisteredTypeIds 를 먼저 정리하여 "마지막 컴파일이 이긴다" 정책 유지.
        /// </summary>
        [RelayCommand]
        private void CompileAllSnippets()
        {
            int succeeded = 0, failed = 0, totalTypes = 0;

            foreach (var snippet in Snippets)
            {
                foreach (var tid in snippet.RegisteredTypeIds)
                {
                    NodeMetadataRegistry.UnregisterForTests(tid);
                }
                snippet.RegisteredTypeIds.Clear();

                var opts = new CompilationOptions { AssemblyName = $"UserCode_{snippet.Category}" };
                opts.ImplicitUsings.Add("System");
                CompilationResult result;
                try
                {
                    result = _compiler.Compile(snippet.SourceCode ?? string.Empty, opts);
                }
                catch (Exception ex)
                {
                    snippet.LastCompile = null;
                    _logger?.Warn($"[Container] Compile threw for snippet '{snippet.Category}': {ex.Message}");
                    failed++;
                    continue;
                }
                snippet.LastCompile = result;

                if (!result.Success || result.Assembly == null)
                {
                    failed++;
                    continue;
                }

                var extracted = UserCodeCategoryExtractor.Extract(result.Assembly);
                if (extracted.PrimaryCategory != null)
                {
                    snippet.Category = extracted.PrimaryCategory;
                }

                // 사전 unregister — [MethodNode] / [ParameterNode] 양쪽 모두 typeId 회수.
                foreach (var t in result.Assembly.GetTypes())
                {
                    foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public
                                                  | System.Reflection.BindingFlags.NonPublic
                                                  | System.Reflection.BindingFlags.Static
                                                  | System.Reflection.BindingFlags.Instance
                                                  | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        var attr = m.GetCustomAttributes(typeof(Core.Scheduler.Attributes.MethodNodeAttribute), false);
                        if (attr.Length == 0) continue;
                        var id = ((Core.Scheduler.Attributes.MethodNodeAttribute)attr[0]).Id;
                        NodeMetadataRegistry.UnregisterForTests(id);
                    }
                    foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public
                                                  | System.Reflection.BindingFlags.Static
                                                  | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        var attr = f.GetCustomAttributes(typeof(Core.Scheduler.Attributes.ParameterNodeAttribute), false);
                        if (attr.Length == 0) continue;
                        var id = ((Core.Scheduler.Attributes.ParameterNodeAttribute)attr[0]).Id;
                        NodeMetadataRegistry.UnregisterForTests(id);
                    }
                }

                var registered = CustomNodeFactory.RegisterFromAssembly(result.Assembly);
                snippet.RegisteredTypeIds.AddRange(registered);
                succeeded++;
                totalTypes += registered.Count;
            }

            // 모든 워크스페이스 팔레트 동기화.
            foreach (var ws in Workspaces)
            {
                ws.RefreshPaletteCategories();
            }

            ContainerStatusMessage = failed == 0
                ? $"Compiled {succeeded} snippet(s), {totalTypes} node type(s) registered."
                : $"Compiled {succeeded} OK, {failed} failed — {totalTypes} node type(s) registered.";
        }

        /// <summary>Edit Code… — 사용자 코드 에디터 윈도우 오픈. DialogParameter 로 이 컨테이너를 전달.</summary>
        [RelayCommand]
        private void OpenUserCodeEditor()
        {
            if (_windowService == null) { ContainerStatusMessage = "Window service not available."; return; }
            _windowService.ShowWindow<object, SchedulerDemoViewModel>(
                nameof(Views.UserCodeEditorWindow), 1100, 720, this);
        }

        /// <summary>Save Code… — 모든 Snippets 를 단일 JSON 파일로 저장. 디폴트 경로 = %APPDATA%/VSMVVM.WPF.Sample/userCode.json.</summary>
        [RelayCommand]
        private void SaveCodeJson()
        {
            if (_fileDialog == null) { ContainerStatusMessage = "File dialog service not available."; return; }
            try
            {
                var defaultPath = GetDefaultUserCodePath();
                var path = _fileDialog.SaveFile("JSON files (*.json)|*.json|All files (*.*)|*.*",
                                                suggestedName: defaultPath);
                if (string.IsNullOrEmpty(path)) return;

                var dict = new Dictionary<string, string>(Snippets.Count);
                foreach (var s in Snippets)
                {
                    dict[s.Category] = s.SourceCode ?? string.Empty;
                }
                var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                EnsureDirectoryFor(path);
                File.WriteAllText(path, json, Encoding.UTF8);
                ContainerStatusMessage = $"Saved {Snippets.Count} snippet(s) to {Path.GetFileName(path)}.";
            }
            catch (Exception ex) { ContainerStatusMessage = $"Save code failed: {ex.Message}"; }
        }

        /// <summary>Load Code… — JSON 파일에서 Snippets 복원 후 자동 컴파일.</summary>
        [RelayCommand]
        private void LoadCodeJson()
        {
            if (_fileDialog == null) { ContainerStatusMessage = "File dialog service not available."; return; }
            try
            {
                var path = _fileDialog.OpenFile("JSON files (*.json)|*.json|All files (*.*)|*.*");
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                var json = File.ReadAllText(path, Encoding.UTF8);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

                // 기존 Snippets 정리 — RegisteredTypeIds 도 NodeMetadataRegistry 에서 unregister.
                foreach (var existing in Snippets)
                {
                    foreach (var tid in existing.RegisteredTypeIds)
                    {
                        NodeMetadataRegistry.UnregisterForTests(tid);
                    }
                }
                Snippets.Clear();
                foreach (var kv in dict)
                {
                    Snippets.Add(new UserCodeSnippet(kv.Key, kv.Value));
                }

                // 자동 컴파일 — 다음 워크스페이스 LoadJson 이 typeId 를 찾을 수 있도록.
                CompileAllSnippetsCommand.Execute(null);
                ContainerStatusMessage = $"Loaded {Snippets.Count} snippet(s) from {Path.GetFileName(path)}.";
            }
            catch (Exception ex) { ContainerStatusMessage = $"Load code failed: {ex.Message}"; }
        }

        private static string GetDefaultUserCodePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VSMVVM.WPF.Sample");
            return Path.Combine(dir, "userCode.json");
        }

        private static void EnsureDirectoryFor(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        // ============= Sample-specific 노드 등록 =============

        private static int _sampleNodesRegistered;
        private static void RegisterSampleNodes()
        {
            if (System.Threading.Interlocked.Exchange(ref _sampleNodesRegistered, 1) != 0) return;
            NodeMetadataRegistry.UnregisterForTests(OutputCaptureNode.TypeIdConst);
            NodeMetadataRegistry.Register(OutputCaptureNode.CreateMetadata());
            NodeMetadataRegistry.UnregisterForTests(ImageViewNode.TypeIdConst);
            NodeMetadataRegistry.Register(ImageViewNode.CreateMetadata());
        }
    }
}
