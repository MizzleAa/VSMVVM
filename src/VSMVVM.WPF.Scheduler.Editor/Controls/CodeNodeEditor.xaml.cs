using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using VSMVVM.Core.Scheduler.Compilation;
using VSMVVM.WPF.Scheduler.Editor.Completion;
using VSMVVM.WPF.Scheduler.Editor.Renderers;
using VSMVVM.WPF.Scheduler.Editor.Themes;

namespace VSMVVM.WPF.Scheduler.Editor.Controls
{
    /// <summary>
    /// AvalonEdit 호스팅 C# 코드 에디터. VSMVVM.WPF.Design 토큰 기반 스타일.
    ///
    /// Phase 8a 범위:
    ///   - SourceCode 양방향 바인딩 (TextChanged → 의존성 프로퍼티)
    ///   - Theme(Dark/Light) DP → XSHD 신택스 하이라이팅 적용
    ///   - SourceCodeCommitted 이벤트 (LostFocus 시)
    ///   - Diagnostics DP 표면 정의 (Phase 8b의 인라인 진단 어도너용 — 현재는 보관만)
    /// </summary>
    public partial class CodeNodeEditor : UserControl
    {
        public static readonly DependencyProperty SourceCodeProperty =
            DependencyProperty.Register(nameof(SourceCode), typeof(string), typeof(CodeNodeEditor),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSourceCodeChanged));

        public static readonly DependencyProperty ThemeProperty =
            DependencyProperty.Register(nameof(Theme), typeof(EditorTheme), typeof(CodeNodeEditor),
                new PropertyMetadata(EditorTheme.Dark, OnThemeChanged));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(CodeNodeEditor),
                new PropertyMetadata(false, OnIsReadOnlyChanged));

        public static readonly DependencyProperty DiagnosticsProperty =
            DependencyProperty.Register(nameof(Diagnostics), typeof(IReadOnlyList<CompilationDiagnostic>),
                typeof(CodeNodeEditor),
                new PropertyMetadata(Array.Empty<CompilationDiagnostic>(), OnDiagnosticsChanged));

        public static readonly DependencyProperty BreakpointsProperty =
            DependencyProperty.Register(nameof(Breakpoints), typeof(IReadOnlyCollection<int>),
                typeof(CodeNodeEditor),
                new PropertyMetadata(Array.Empty<int>(), OnBreakpointsChanged));

        public static readonly DependencyProperty ShowCurrentLineHighlightProperty =
            DependencyProperty.Register(nameof(ShowCurrentLineHighlight), typeof(bool), typeof(CodeNodeEditor),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowBreakpointMarginProperty =
            DependencyProperty.Register(nameof(ShowBreakpointMargin), typeof(bool), typeof(CodeNodeEditor),
                new PropertyMetadata(true, OnShowBreakpointMarginChanged));

        public static readonly DependencyProperty EnableCompletionProperty =
            DependencyProperty.Register(nameof(EnableCompletion), typeof(bool), typeof(CodeNodeEditor),
                new PropertyMetadata(true));

        /// <summary>편집 중인 C# 소스 코드. 양방향 바인딩.</summary>
        public string SourceCode
        {
            get => (string)GetValue(SourceCodeProperty);
            set => SetValue(SourceCodeProperty, value);
        }

        /// <summary>신택스 하이라이팅 테마.</summary>
        public EditorTheme Theme
        {
            get => (EditorTheme)GetValue(ThemeProperty);
            set => SetValue(ThemeProperty, value);
        }

        /// <summary>읽기 전용 모드.</summary>
        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        /// <summary>인라인 진단 표시용 컬렉션 — DiagnosticUnderlineRenderer가 sine-wave 밑줄로 렌더링.</summary>
        public IReadOnlyList<CompilationDiagnostic> Diagnostics
        {
            get => (IReadOnlyList<CompilationDiagnostic>)GetValue(DiagnosticsProperty);
            set => SetValue(DiagnosticsProperty, value);
        }

        /// <summary>활성 브레이크포인트 줄 번호(1-based) 컬렉션.</summary>
        public IReadOnlyCollection<int> Breakpoints
        {
            get => (IReadOnlyCollection<int>)GetValue(BreakpointsProperty);
            set => SetValue(BreakpointsProperty, value);
        }

        /// <summary>현재 캐럿이 있는 줄의 배경 강조 표시 여부.</summary>
        public bool ShowCurrentLineHighlight
        {
            get => (bool)GetValue(ShowCurrentLineHighlightProperty);
            set => SetValue(ShowCurrentLineHighlightProperty, value);
        }

        /// <summary>좌측 브레이크포인트 마진 표시 여부.</summary>
        public bool ShowBreakpointMargin
        {
            get => (bool)GetValue(ShowBreakpointMarginProperty);
            set => SetValue(ShowBreakpointMarginProperty, value);
        }

        /// <summary>Roslyn 자동완성 활성 여부. true면 '.' 입력 / Ctrl+Space 시 CompletionWindow 표시.</summary>
        public bool EnableCompletion
        {
            get => (bool)GetValue(EnableCompletionProperty);
            set => SetValue(EnableCompletionProperty, value);
        }

        /// <summary>편집 결과를 외부에 통보. 기본적으로 LostFocus 시 발화.</summary>
        public event EventHandler<string> SourceCodeCommitted;

        /// <summary>사용자가 마진 클릭으로 브레이크포인트를 토글했을 때 발화.</summary>
        public event EventHandler<BreakpointToggledEventArgs> BreakpointToggled;

        /// <summary>외부에서 직접 조작이 필요할 때 노출 (예: 캐럿 위치 이동, 폴딩 등).</summary>
        public TextEditor InnerEditor => Editor;

        private bool _suppressTextChange;
        private DiagnosticUnderlineRenderer _diagnosticRenderer;
        private CurrentLineHighlighter _currentLineRenderer;
        private BreakpointMargin _breakpointMargin;
        private RoslynCompletionProvider _completionProvider;
        private CompletionWindow _completionWindow;

        public CodeNodeEditor()
        {
            InitializeComponent();
            Editor.TextChanged += OnEditorTextChanged;
            Editor.LostFocus += OnEditorLostFocus;
            ApplyTheme(Theme);
            InstallRenderers();
            InstallBreakpointMargin();
            InstallCompletion();
        }

        private void InstallCompletion()
        {
            _completionProvider = new RoslynCompletionProvider();
            // 키 입력 후 트리거 (TextEntered = 새 문자 입력 후) + 키다운에서 Ctrl+Space 감지
            Editor.TextArea.TextEntered += OnTextEntered;
            Editor.TextArea.KeyDown += OnTextAreaKeyDown;
            Unloaded += (_, __) => DisposeCompletion();
        }

        // 진행 중인 자동완성 호출 — 사용자가 빠르게 다음 키를 누르면 이전 호출을 취소.
        private System.Threading.CancellationTokenSource _completionCts;
        // 디바운스: '.' 입력 후 짧은 시간 내 추가 키가 오면 마지막 키 기준으로만 1회 호출.
        private const int CompletionDebounceMs = 100;

        private async void OnTextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (!EnableCompletion) return;
            if (e.Text.Length != 1) return;
            var ch = e.Text[0];
            // PoC: '.' 입력 시만 자동 트리거 (식별자 입력 중 자동 표시는 부담스러우므로 제외)
            if (ch == '.')
            {
                await ShowCompletionAsync(CompletionTriggerKind.Insertion, ch, debounceMs: CompletionDebounceMs)
                    .ConfigureAwait(true);
            }
        }

        private async void OnTextAreaKeyDown(object sender, KeyEventArgs e)
        {
            if (!EnableCompletion) return;
            // Ctrl+Space → 명시 호출. 디바운스 없이 즉시.
            if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                await ShowCompletionAsync(CompletionTriggerKind.Invoke, '\0', debounceMs: 0)
                    .ConfigureAwait(true);
            }
        }

        private async System.Threading.Tasks.Task ShowCompletionAsync(CompletionTriggerKind kind, char triggerChar, int debounceMs)
        {
            // 이전 진행 호출 취소
            _completionCts?.Cancel();
            _completionCts = new System.Threading.CancellationTokenSource();
            var token = _completionCts.Token;

            // 기존 창이 떠 있으면 닫기
            _completionWindow?.Close();
            _completionWindow = null;

            // 디바운스 — 빠른 연속 호출 시 마지막 입력만 처리
            if (debounceMs > 0)
            {
                try { await System.Threading.Tasks.Task.Delay(debounceMs, token).ConfigureAwait(true); }
                catch (System.Threading.Tasks.TaskCanceledException) { return; }
            }
            if (token.IsCancellationRequested) return;

            var pos = Editor.CaretOffset;
            var text = Editor.Text ?? string.Empty;

            IReadOnlyList<RoslynCompletionItem> items;
            try
            {
                // Roslyn 호출은 UI 스레드 컨텍스트를 잡지 않도록 ConfigureAwait(false) —
                // 완료 후 결과만 UI 스레드로 marshal (Show 호출).
                items = await _completionProvider
                    .GetCompletionsAsync(text, pos, kind, triggerChar, token)
                    .ConfigureAwait(false);
            }
            catch (System.OperationCanceledException) { return; }
            catch { return; /* 자동완성 실패는 silent — 사용자 편집을 막지 않음 */ }

            if (token.IsCancellationRequested) return;
            if (items == null || items.Count == 0) return;

            // 결과를 UI 스레드로 marshal — Dispatcher가 자동으로 UI 스레드를 잡음.
            await Dispatcher.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested) return;

                _completionWindow = new CompletionWindow(Editor.TextArea);
                // CompletionWindow에 우리 디자인 시스템 리소스 머지 — 머지된 ResourceDictionary가
                // 자동 적용된다 (Style TargetType은 동일 타입이면 적용됨).
                _completionWindow.Resources.MergedDictionaries.Add(
                    new ResourceDictionary { Source = new Uri("/VSMVVM.WPF.Scheduler.Editor;component/Themes/Generic.xaml", UriKind.Relative) });

                foreach (var item in items)
                {
                    _completionWindow.CompletionList.CompletionData.Add(new AvalonCompletionData(item));
                }
                _completionWindow.Closed += (_, __) => _completionWindow = null;
                _completionWindow.Show();
            });
        }

        private void DisposeCompletion()
        {
            try
            {
                _completionWindow?.Close();
            }
            catch { }
            _completionWindow = null;
            _completionProvider?.Dispose();
            _completionProvider = null;
        }

        private void InstallRenderers()
        {
            _diagnosticRenderer = new DiagnosticUnderlineRenderer(
                diagnosticsProvider: () => Diagnostics,
                errorBrushProvider: () => TryFindResource("Error") as Brush,
                warningBrushProvider: () => TryFindResource("Warning") as Brush,
                infoBrushProvider: () => TryFindResource("Info") as Brush);
            Editor.TextArea.TextView.BackgroundRenderers.Add(_diagnosticRenderer);

            _currentLineRenderer = new CurrentLineHighlighter(
                brushProvider: () => TryFindResource("BgSecondary") as Brush);
            Editor.TextArea.TextView.BackgroundRenderers.Add(_currentLineRenderer);

            // 캐럿이 다른 줄로 이동하면 현재 줄 하이라이트 재그림
            Editor.TextArea.Caret.PositionChanged += (_, __) =>
            {
                if (ShowCurrentLineHighlight) Editor.TextArea.TextView.InvalidateLayer(_currentLineRenderer.Layer);
            };
        }

        private void InstallBreakpointMargin()
        {
            _breakpointMargin = new BreakpointMargin(
                brushProvider: () => TryFindResource("Error") as Brush);
            _breakpointMargin.BreakpointToggled += OnMarginBreakpointToggled;
            UpdateBreakpointMarginVisibility();
        }

        private void OnMarginBreakpointToggled(object sender, BreakpointToggledEventArgs e)
        {
            // DP 동기화 (외부 바인딩이 양방향이라면 노트북에 반영)
            SetCurrentValue(BreakpointsProperty, _breakpointMargin.Breakpoints.ToArray());
            BreakpointToggled?.Invoke(this, e);
        }

        private void UpdateBreakpointMarginVisibility()
        {
            var margins = Editor.TextArea.LeftMargins;
            if (ShowBreakpointMargin)
            {
                if (!margins.Contains(_breakpointMargin)) margins.Insert(0, _breakpointMargin);
            }
            else
            {
                margins.Remove(_breakpointMargin);
            }
        }

        private static void OnSourceCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ed = (CodeNodeEditor)d;
            var newText = (string)e.NewValue ?? string.Empty;
            if (ed.Editor.Text == newText) return;
            ed._suppressTextChange = true;
            try { ed.Editor.Text = newText; }
            finally { ed._suppressTextChange = false; }
        }

        private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ed = (CodeNodeEditor)d;
            ed.ApplyTheme((EditorTheme)e.NewValue);
        }

        private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ed = (CodeNodeEditor)d;
            ed.Editor.IsReadOnly = (bool)e.NewValue;
        }

        private void OnEditorTextChanged(object sender, EventArgs e)
        {
            if (_suppressTextChange) return;
            SetCurrentValue(SourceCodeProperty, Editor.Text);
        }

        private void OnEditorLostFocus(object sender, RoutedEventArgs e)
        {
            SourceCodeCommitted?.Invoke(this, Editor.Text);
        }

        private void ApplyTheme(EditorTheme theme)
        {
            Editor.SyntaxHighlighting = HighlightingThemeService.Load(theme);
        }

        private static void OnDiagnosticsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ed = (CodeNodeEditor)d;
            // 진단이 바뀌면 Selection 레이어를 무효화하여 DiagnosticUnderlineRenderer가 다시 그리도록.
            if (ed._diagnosticRenderer != null)
                ed.Editor.TextArea.TextView.InvalidateLayer(ed._diagnosticRenderer.Layer);
        }

        private static void OnBreakpointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ed = (CodeNodeEditor)d;
            if (ed._breakpointMargin == null) return;
            ed._breakpointMargin.SetBreakpoints((IReadOnlyCollection<int>)e.NewValue ?? Array.Empty<int>());
        }

        private static void OnShowBreakpointMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ed = (CodeNodeEditor)d;
            ed.UpdateBreakpointMarginVisibility();
        }
    }
}
