using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;

namespace VSMVVM.WPF.Scheduler.Editor.Renderers
{
    /// <summary>
    /// 현재 캐럿이 있는 시각 라인의 배경을 가볍게 강조한다 (반투명).
    /// 색은 호스트가 토큰 lookup으로 제공 (기본은 BgSecondary 폴백).
    /// </summary>
    public sealed class CurrentLineHighlighter : IBackgroundRenderer
    {
        private static readonly Brush FallbackBrush = MakeFrozen("#18181B", 0.35); // Zinc900 반투명

        private readonly Func<Brush> _brushProvider;

        public CurrentLineHighlighter(Func<Brush> brushProvider = null)
        {
            _brushProvider = brushProvider ?? (() => FallbackBrush);
        }

        // 텍스트 아래에 깔리도록 Background 레이어
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView == null || drawingContext == null) return;
            if (!textView.VisualLinesValid) return;
            var brush = _brushProvider() ?? FallbackBrush;

            // 캐럿 위치는 TextArea가 알고 있지만 TextView에서는 간접 접근.
            // AvalonEdit 표준 패턴: TextArea의 Caret에 접근하려면 TextView.GetService(typeof(TextArea)) 사용.
            // 여기서는 단순화: GetService로 TextArea를 얻은 뒤 캐럿 줄을 찾는다.
            var textArea = textView.GetService(typeof(ICSharpCode.AvalonEdit.Editing.TextArea))
                as ICSharpCode.AvalonEdit.Editing.TextArea;
            if (textArea == null) return;

            var caretLine = textArea.Caret.Line;
            var doc = textView.Document;
            if (doc == null || caretLine < 1 || caretLine > doc.LineCount) return;

            var line = doc.GetLineByNumber(caretLine);
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, line))
            {
                // 줄 전체 폭으로 확장 (텍스트가 짧아도 뷰포트 끝까지)
                var full = new System.Windows.Rect(rect.X, rect.Y,
                    Math.Max(rect.Width, textView.ActualWidth - rect.X), rect.Height);
                drawingContext.DrawRectangle(brush, null, full);
            }
        }

        private static Brush MakeFrozen(string hex, double opacity = 1.0)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color) { Opacity = opacity };
            brush.Freeze();
            return brush;
        }
    }
}
