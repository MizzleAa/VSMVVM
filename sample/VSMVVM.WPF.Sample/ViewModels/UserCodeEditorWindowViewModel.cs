using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Compilation;
using VSMVVM.WPF.Scheduler.Editor.Themes;
using VSMVVM.WPF.Scheduler.ViewModels;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// 멀티 조각 사용자 C# 코드 편집 다이얼로그 ViewModel. Phase M 이후 전역 (컨테이너 단위).
    /// <para>
    /// DialogParameter 로 <see cref="SchedulerDemoViewModel"/> 를 받아 그 Snippets 를 직접 편집한다 (live).
    /// 모든 워크스페이스가 같은 Snippets / NodeMetadataRegistry 를 공유하므로, 한 번 컴파일하면 모든 탭의 팔레트에 즉시 반영.
    /// </para>
    /// </summary>
    public partial class UserCodeEditorWindowViewModel : ViewModelBase
    {
        private readonly ICompilationService _compiler;
        private CancellationTokenSource _diagnosticsCts;
        private SchedulerDemoViewModel _container;

        /// <summary>편집 대상 컨테이너의 조각 컬렉션. live binding — 다이얼로그 안 변경이 즉시 반영.</summary>
        public ObservableCollection<UserCodeSnippet> Snippets => _container?.Snippets
            ?? _emptySnippets;
        private static readonly ObservableCollection<UserCodeSnippet> _emptySnippets = new();

        [Property]
        [PropertyChangedFor(nameof(HasSelectedSnippet))]
        [PropertyChangedFor(nameof(SelectedSourceCode))]
        [PropertyChangedFor(nameof(SelectedDiagnostics))]
        private UserCodeSnippet _selectedSnippet;

        /// <summary>선택된 조각의 SourceCode 양방향 바인딩 — 편집 시 라이브 진단 트리거.</summary>
        public string SelectedSourceCode
        {
            get => _selectedSnippet?.SourceCode ?? string.Empty;
            set
            {
                if (_selectedSnippet == null) return;
                if (_selectedSnippet.SourceCode == value) return;
                _selectedSnippet.SourceCode = value;
                OnPropertyChanged(nameof(SelectedSourceCode));
                ScheduleDiagnostics(value);
            }
        }

        /// <summary>편집기에 전달할 진단 — 선택 조각의 LastCompile.Diagnostics.</summary>
        public IReadOnlyList<CompilationDiagnostic> SelectedDiagnostics =>
            _selectedSnippet?.LastCompile?.Diagnostics ?? Array.Empty<CompilationDiagnostic>();

        public bool HasSelectedSnippet => _selectedSnippet != null;

        [Property] private EditorTheme _editorTheme = EditorTheme.Dark;
        [Property] private string _status = string.Empty;

        /// <summary>
        /// 호스트가 ShowWindow 시 DialogParameter 로 SchedulerDemoViewModel 을 주입.
        /// </summary>
        public SchedulerDemoViewModel DialogParameter
        {
            get => _container;
            set
            {
                _container = value;
                OnPropertyChanged(nameof(Snippets));
                // 컨테이너가 초기 카테고리를 요청했으면 해당 스니펫으로, 아니면 첫 번째.
                UserCodeSnippet initial = null;
                var hint = _container?.InitialSnippetCategory;
                if (!string.IsNullOrEmpty(hint))
                {
                    initial = _container?.Snippets?.FirstOrDefault(
                        s => string.Equals(s.Category, hint, StringComparison.Ordinal));
                    _container.InitialSnippetCategory = null; // 1회성 소비
                }
                SelectedSnippet = initial ?? _container?.Snippets?.FirstOrDefault();
            }
        }

        public event EventHandler RequestClose;

        public UserCodeEditorWindowViewModel(ICompilationService compiler)
        {
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        }

        partial void OnSelectedSnippetChanged(UserCodeSnippet value)
        {
            ScheduleDiagnostics(value?.SourceCode);
        }

        private void ScheduleDiagnostics(string source)
        {
            _diagnosticsCts?.Cancel();
            _diagnosticsCts = new CancellationTokenSource();
            _ = AnalyzeWithDebounceAsync(source ?? string.Empty, _diagnosticsCts.Token);
        }

        private async Task AnalyzeWithDebounceAsync(string source, CancellationToken token)
        {
            try
            {
                await Task.Delay(300, token).ConfigureAwait(true);
                if (token.IsCancellationRequested) return;
                if (_selectedSnippet == null) return;

                var opts = new CompilationOptions { AssemblyName = $"SnippetLiveDiag_{_selectedSnippet.Category}" };
                opts.ImplicitUsings.Add("System");
                var diags = _compiler.Analyze(source, opts);

                _selectedSnippet.LastCompile = new CompilationResult(
                    success: !diags.Any(d => d.Severity == CompilationDiagnosticSeverity.Error),
                    assembly: _selectedSnippet.LastCompile?.Assembly,
                    diagnostics: diags);
                OnPropertyChanged(nameof(SelectedDiagnostics));
            }
            catch (TaskCanceledException) { }
            catch { /* 진단 실패는 무시 */ }
        }

        // ============= 명령 =============

        [RelayCommand]
        private void AddSnippet()
        {
            if (_container == null) return;
            int n = _container.Snippets.Count(s => s.Category.StartsWith("Untitled", StringComparison.Ordinal)) + 1;
            var snippet = new UserCodeSnippet($"Untitled {n}", DefaultNewSnippetSource);
            _container.Snippets.Add(snippet);
            SelectedSnippet = snippet;
            Status = $"Added new snippet 'Untitled {n}'. Edit then Compile to auto-classify category.";
        }

        [RelayCommand]
        private void RemoveSelectedSnippet()
        {
            if (_container == null || _selectedSnippet == null) return;
            var snippet = _selectedSnippet;

            foreach (var tid in snippet.RegisteredTypeIds)
            {
                Core.Scheduler.Nodes.NodeMetadataRegistry.UnregisterForTests(tid);
            }
            snippet.RegisteredTypeIds.Clear();

            var idx = _container.Snippets.IndexOf(snippet);
            _container.Snippets.Remove(snippet);

            if (_container.Snippets.Count > 0)
            {
                var nextIdx = Math.Min(idx, _container.Snippets.Count - 1);
                SelectedSnippet = _container.Snippets[nextIdx];
            }
            else
            {
                SelectedSnippet = null;
            }

            // 모든 워크스페이스 팔레트 갱신.
            foreach (var ws in _container.Workspaces)
            {
                ws.RefreshPaletteCategories();
            }
            Status = $"Removed snippet. {snippet.RegisteredTypeIds.Count} node type(s) unregistered.";
        }

        [RelayCommand]
        private void CompileThis()
        {
            if (_container == null || _selectedSnippet == null) return;
            CompileOne(_selectedSnippet);
            foreach (var ws in _container.Workspaces)
            {
                ws.RefreshPaletteCategories();
            }
        }

        [RelayCommand]
        private void CompileAll()
        {
            if (_container == null) return;
            _container.CompileAllSnippetsCommand?.Execute(null);
            Status = _container.ContainerStatusMessage;
            OnPropertyChanged(nameof(SelectedDiagnostics));
        }

        private void CompileOne(UserCodeSnippet snippet)
        {
            foreach (var tid in snippet.RegisteredTypeIds)
            {
                Core.Scheduler.Nodes.NodeMetadataRegistry.UnregisterForTests(tid);
            }
            snippet.RegisteredTypeIds.Clear();

            var opts = new CompilationOptions { AssemblyName = $"UserCode_{snippet.Category}" };
            opts.ImplicitUsings.Add("System");
            CompilationResult result;
            try { result = _compiler.Compile(snippet.SourceCode ?? string.Empty, opts); }
            catch (Exception ex)
            {
                Status = $"Compile threw: {ex.Message}";
                snippet.LastCompile = null;
                OnPropertyChanged(nameof(SelectedDiagnostics));
                return;
            }
            snippet.LastCompile = result;
            OnPropertyChanged(nameof(SelectedDiagnostics));

            if (!result.Success || result.Assembly == null)
            {
                var firstErr = result.Diagnostics.FirstOrDefault(d => d.Severity == CompilationDiagnosticSeverity.Error);
                Status = firstErr != null
                    ? $"Compile FAILED: {firstErr.Id} {firstErr.Message}"
                    : "Compile FAILED — see diagnostics.";
                return;
            }

            var extracted = Core.Scheduler.Nodes.UserCodeCategoryExtractor.Extract(result.Assembly);
            if (extracted.PrimaryCategory != null) snippet.Category = extracted.PrimaryCategory;

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
                    Core.Scheduler.Nodes.NodeMetadataRegistry.UnregisterForTests(id);
                }
                foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public
                                              | System.Reflection.BindingFlags.Static
                                              | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    var attr = f.GetCustomAttributes(typeof(Core.Scheduler.Attributes.ParameterNodeAttribute), false);
                    if (attr.Length == 0) continue;
                    var id = ((Core.Scheduler.Attributes.ParameterNodeAttribute)attr[0]).Id;
                    Core.Scheduler.Nodes.NodeMetadataRegistry.UnregisterForTests(id);
                }
            }

            var registered = Core.Scheduler.Nodes.CustomNodeFactory.RegisterFromAssembly(result.Assembly);
            snippet.RegisteredTypeIds.AddRange(registered);
            Status = $"Compiled '{snippet.Category}' — {registered.Count} node type(s) registered.";
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            EditorTheme = EditorTheme == EditorTheme.Dark ? EditorTheme.Light : EditorTheme.Dark;
        }

        [RelayCommand]
        private void Close()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private const string DefaultNewSnippetSource = @"// 새 코드 조각. [MethodNode(Category=...)] / [ParameterNode(Category=...)] 의 Category 값이
// 자동으로 이 조각의 카테고리 이름이 됩니다. 컴파일 후 좌측 리스트가 갱신됩니다.

using System;
using VSMVVM.Core.Scheduler.Attributes;

namespace Demo.MyCategory
{
    public static class MyOps
    {
        [MethodNode(""Demo.MyCategory.Hello"", Category = ""MyCategory"")]
        public static string Hello(string name)
        {
            return ""Hello "" + name + ""!"";
        }
    }
}
";
    }
}
