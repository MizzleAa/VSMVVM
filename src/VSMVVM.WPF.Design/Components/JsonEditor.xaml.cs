using System;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace VSMVVM.WPF.Design.Components
{
    /// <summary>
    /// VSCode 스타일 JSON 에디터 컴포넌트
    /// 구문 강조, 라인 번호, Tab/Enter 자동 들여쓰기, 실시간 JSON 검증을 제공합니다.
    /// </summary>
    public partial class JsonEditor : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        #region Constants

        /// <summary>Tab 키 삽입 시 사용할 공백 수</summary>
        private const int TabSpaceCount = 4;

        /// <summary>Tab 문자열 (스페이스 기반)</summary>
        private static readonly string TabString = new(' ', TabSpaceCount);

        /// <summary>유효한 JSON 상태 메시지</summary>
        private const string ValidJsonMessage = "Valid JSON";

        /// <summary>구문 강조 디바운스 간격 (밀리초)</summary>
        private const int HighlightDebounceMs = 400;

        /// <summary>JSON 검증 디바운스 간격 (밀리초)</summary>
        private const int ValidationDebounceMs = 500;

        #endregion

        #region Syntax Highlighting Colors

        /// <summary>JSON 키 (속성명) 색상 — Sky400</summary>
        private static readonly SolidColorBrush KeyBrush = new(Color.FromRgb(56, 189, 248));

        /// <summary>JSON 문자열 값 색상 — Amber300</summary>
        private static readonly SolidColorBrush StringValueBrush = new(Color.FromRgb(252, 211, 77));

        /// <summary>JSON 숫자 값 색상 — Lime400</summary>
        private static readonly SolidColorBrush NumberBrush = new(Color.FromRgb(163, 230, 53));

        /// <summary>JSON Boolean 값 색상 — Purple400</summary>
        private static readonly SolidColorBrush BooleanBrush = new(Color.FromRgb(192, 132, 252));

        /// <summary>JSON null 값 색상 — Red400</summary>
        private static readonly SolidColorBrush NullBrush = new(Color.FromRgb(248, 113, 113));

        /// <summary>JSON 구조 기호 ({}, []) 색상 — Zinc400</summary>
        private static readonly SolidColorBrush BracketBrush = new(Color.FromRgb(161, 161, 170));

        /// <summary>JSON 구분 기호 (:, ,) 색상 — Zinc500</summary>
        private static readonly SolidColorBrush PunctuationBrush = new(Color.FromRgb(113, 113, 122));

        /// <summary>기본 텍스트 색상 — Zinc300</summary>
        private static readonly SolidColorBrush DefaultTextBrush = new(Color.FromRgb(212, 212, 216));

        #endregion

        #region Regex Patterns

        private static readonly Regex KeyPattern = new(
            "\"(?<key>[^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\"\\s*:",
            RegexOptions.Compiled);

        private static readonly Regex StringValuePattern = new(
            ":\\s*\"(?<val>[^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\"",
            RegexOptions.Compiled);

        private static readonly Regex NumberPattern = new(
            @"(?<=[:,\[\s])\s*-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?",
            RegexOptions.Compiled);

        private static readonly Regex BooleanPattern = new(
            @"\b(true|false)\b",
            RegexOptions.Compiled);

        private static readonly Regex NullPattern = new(
            @"\bnull\b",
            RegexOptions.Compiled);

        private static readonly Regex BracketPattern = new(
            @"[\{\}\[\]]",
            RegexOptions.Compiled);

        private static readonly Regex PunctuationPattern = new(
            @"[:,]",
            RegexOptions.Compiled);

        #endregion

        #region DependencyProperties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text), typeof(string), typeof(JsonEditor),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnTextPropertyChanged));

        public static readonly DependencyProperty IsEditorReadOnlyProperty =
            DependencyProperty.Register(
                nameof(IsEditorReadOnly), typeof(bool), typeof(JsonEditor),
                new PropertyMetadata(false, OnIsReadOnlyChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IsEditorReadOnly
        {
            get => (bool)GetValue(IsEditorReadOnlyProperty);
            set => SetValue(IsEditorReadOnlyProperty, value);
        }

        #endregion

        #region Fields

        private bool _isSyncing;
        private System.Threading.Timer _highlightTimer;
        private System.Threading.Timer _validationTimer;

        #endregion

        #region Constructor

        public JsonEditor()
        {
            InitializeComponent();
            Loaded += OnControlLoaded;
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Text))
            {
                SetEditorText(Text);
            }
        }

        #endregion

        #region DependencyProperty Callbacks

        private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is JsonEditor editor && !editor._isSyncing && editor.IsLoaded)
            {
                editor.SetEditorText((string)e.NewValue);
            }
        }

        private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is JsonEditor editor)
            {
                editor.EditorBox.IsReadOnly = (bool)e.NewValue;
            }
        }

        #endregion

        #region Editor - Text Sync

        private void SetEditorText(string text)
        {
            _isSyncing = true;
            try
            {
                EditorBox.Document.Blocks.Clear();
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run(text ?? string.Empty));
                EditorBox.Document.Blocks.Add(paragraph);
                ScheduleHighlighting();
                UpdateLineNumbers();
                ValidateJson(text ?? string.Empty);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private string GetEditorText()
        {
            var range = new TextRange(
                EditorBox.Document.ContentStart,
                EditorBox.Document.ContentEnd);
            var text = range.Text;
            if (text.EndsWith("\r\n"))
            {
                text = text[..^2];
            }
            return text;
        }

        #endregion

        #region Event Handlers

        private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncing) return;

            _isSyncing = true;
            try
            {
                Text = GetEditorText();
            }
            finally
            {
                _isSyncing = false;
            }

            ScheduleHighlighting();
            UpdateLineNumbers();
            ScheduleValidation();
        }

        private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (e.Key == Key.Tab)
            {
                HandleTabKey(e);
            }
            else if (e.Key == Key.Return)
            {
                HandleEnterKey(e);
            }
            else if (isCtrl && isShift && e.Key == Key.Z)
            {
                e.Handled = true;
                if (EditorBox.CanRedo) EditorBox.Redo();
            }
        }

        private void OnEditorScrollChanged(object sender, ScrollChangedEventArgs e) { }

        private void OnFormatClick(object sender, RoutedEventArgs e)
        {
            FormatJson();
        }

        #endregion

        #region Tab Key

        private void HandleTabKey(KeyEventArgs e)
        {
            e.Handled = true;
            if (EditorBox.CaretPosition is null) return;

            EditorBox.CaretPosition.InsertTextInRun(TabString);
            EditorBox.CaretPosition = EditorBox.CaretPosition.GetPositionAtOffset(TabSpaceCount);
        }

        #endregion

        #region Enter Key (Auto-Indent)

        private void HandleEnterKey(KeyEventArgs e)
        {
            e.Handled = true;

            var caretPos = EditorBox.CaretPosition;
            if (caretPos is null) return;

            var currentLineText = GetCurrentLineText(caretPos);
            var indent = GetLeadingWhitespace(currentLineText);
            var trimmedLine = currentLineText.TrimEnd();

            if (trimmedLine.EndsWith('{') || trimmedLine.EndsWith('['))
            {
                indent += TabString;
            }

            var newLineText = "\r\n" + indent;
            caretPos.InsertTextInRun(newLineText);

            var newPos = caretPos.GetPositionAtOffset(newLineText.Length);
            if (newPos is not null) EditorBox.CaretPosition = newPos;
        }

        private static string GetCurrentLineText(TextPointer caretPos)
        {
            var lineStart = caretPos.GetLineStartPosition(0);
            if (lineStart is null) return string.Empty;
            return new TextRange(lineStart, caretPos).Text;
        }

        private static string GetLeadingWhitespace(string text)
        {
            var count = 0;
            foreach (var ch in text)
            {
                if (ch is ' ' or '\t') count++;
                else break;
            }
            return text[..count];
        }

        #endregion

        #region Syntax Highlighting

        private void ScheduleHighlighting()
        {
            _highlightTimer?.Dispose();
            _highlightTimer = new System.Threading.Timer(
                _ => Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(ApplySyntaxHighlighting)),
                null,
                HighlightDebounceMs,
                System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// 최적화된 구문 강조 — BeginChange/EndChange로 중간 레이아웃 억제,
        /// 텍스트를 1회만 추출하여 모든 패턴에 재사용
        /// </summary>
        private void ApplySyntaxHighlighting()
        {
            _isSyncing = true;
            try
            {
                var text = GetEditorText();
                if (string.IsNullOrEmpty(text)) return;

                var docStart = EditorBox.Document.ContentStart;

                // BeginChange로 모든 포맷팅을 단일 트랜잭션으로 처리
                // → 중간 레이아웃 재계산 억제 (핵심 성능 개선)
                EditorBox.BeginChange();
                try
                {
                    var fullRange = new TextRange(
                        EditorBox.Document.ContentStart,
                        EditorBox.Document.ContentEnd);
                    fullRange.ApplyPropertyValue(TextElement.ForegroundProperty, DefaultTextBrush);

                    // 캐시된 text와 docStart를 모든 패턴에 재사용
                    ApplyPatternHighlight(text, docStart, BracketPattern, BracketBrush);
                    ApplyPatternHighlight(text, docStart, PunctuationPattern, PunctuationBrush);
                    ApplyPatternHighlight(text, docStart, NumberPattern, NumberBrush);
                    ApplyPatternHighlight(text, docStart, BooleanPattern, BooleanBrush);
                    ApplyPatternHighlight(text, docStart, NullPattern, NullBrush);
                    ApplyStringValueHighlight(text, docStart);
                    ApplyKeyHighlight(text, docStart);
                }
                finally
                {
                    EditorBox.EndChange();
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private static void ApplyPatternHighlight(string text, TextPointer docStart, Regex pattern, SolidColorBrush brush)
        {
            foreach (Match match in pattern.Matches(text))
            {
                var start = GetTextPointerAtOffset(docStart, match.Index);
                var end = GetTextPointerAtOffset(docStart, match.Index + match.Length);

                if (start is not null && end is not null)
                {
                    new TextRange(start, end).ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                }
            }
        }

        private static void ApplyKeyHighlight(string text, TextPointer docStart)
        {
            foreach (Match match in KeyPattern.Matches(text))
            {
                var keyGroup = match.Groups["key"];
                var quoteStart = match.Index;
                var quoteEnd = quoteStart + keyGroup.Length + 2;

                var start = GetTextPointerAtOffset(docStart, quoteStart);
                var end = GetTextPointerAtOffset(docStart, quoteEnd);

                if (start is not null && end is not null)
                {
                    new TextRange(start, end).ApplyPropertyValue(TextElement.ForegroundProperty, KeyBrush);
                }
            }
        }

        private static void ApplyStringValueHighlight(string text, TextPointer docStart)
        {
            foreach (Match match in StringValuePattern.Matches(text))
            {
                var valStart = match.Index + match.Value.IndexOf('"');
                var valEnd = match.Index + match.Length;

                var start = GetTextPointerAtOffset(docStart, valStart);
                var end = GetTextPointerAtOffset(docStart, valEnd);

                if (start is not null && end is not null)
                {
                    new TextRange(start, end).ApplyPropertyValue(TextElement.ForegroundProperty, StringValueBrush);
                }
            }
        }

        private static TextPointer GetTextPointerAtOffset(TextPointer start, int offset)
        {
            var current = start;
            var remaining = offset;

            while (current is not null)
            {
                var context = current.GetPointerContext(LogicalDirection.Forward);

                if (context == TextPointerContext.Text)
                {
                    var runLength = current.GetTextRunLength(LogicalDirection.Forward);
                    if (remaining <= runLength)
                    {
                        return current.GetPositionAtOffset(remaining);
                    }
                    remaining -= runLength;
                }

                current = current.GetNextContextPosition(LogicalDirection.Forward);
            }

            return null;
        }

        #endregion

        #region Line Numbers

        private void UpdateLineNumbers()
        {
            if (!IsLoaded) return;

            var text = GetEditorText();
            var lineCount = string.IsNullOrEmpty(text) ? 1 : text.Split('\n').Length;
            LineNumbersPanel.ItemsSource = Enumerable.Range(1, lineCount).ToList();
        }

        #endregion

        #region JSON Validation

        private void ScheduleValidation()
        {
            _validationTimer?.Dispose();
            _validationTimer = new System.Threading.Timer(
                _ => Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => ValidateJson(Text))),
                null,
                ValidationDebounceMs,
                System.Threading.Timeout.Infinite);
        }

        private void ValidateJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                SetValidationStatus(true, ValidJsonMessage);
                return;
            }

            try
            {
                JToken.Parse(text);
                SetValidationStatus(true, ValidJsonMessage);
            }
            catch (JsonReaderException ex)
            {
                SetValidationStatus(false, $"Line {ex.LineNumber}, Pos {ex.LinePosition}: {ex.Message}");
            }
        }

        private void SetValidationStatus(bool isValid, string message)
        {
            if (!IsLoaded) return;

            var validBrush = isValid
                ? (Brush)FindResource("Emerald500Brush")
                : (Brush)FindResource("Red500Brush");

            ValidationIndicator.Fill = validBrush;
            ValidationMessage.Foreground = isValid
                ? (Brush)FindResource("TextMuted")
                : validBrush;
            ValidationMessage.Text = message;
        }

        #endregion

        #region Format JSON

        private void FormatJson()
        {
            var text = GetEditorText();
            if (string.IsNullOrWhiteSpace(text)) return;

            try
            {
                var token = JToken.Parse(text);
                Text = token.ToString(Formatting.Indented);
            }
            catch (JsonReaderException) { }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}
