using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace VSMVVM.WPF.Scheduler.Editor.Completion
{
    /// <summary>RoslynCompletionItem 을 AvalonEdit 의 ICompletionData 로 어댑팅.</summary>
    internal sealed class AvalonCompletionData : ICompletionData
    {
        private readonly RoslynCompletionItem _item;

        public AvalonCompletionData(RoslynCompletionItem item)
        {
            _item = item;
        }

        public ImageSource Image => null; // Phase 8c는 텍스트만 — 아이콘은 후속.

        public string Text => _item.InsertionText;

        public object Content => _item.DisplayText;

        public object Description => string.IsNullOrEmpty(_item.Description)
            ? _item.Kind.ToString()
            : $"{_item.Kind}: {_item.Description}";

        public double Priority => 1.0;

        public void Complete(TextArea textArea, ISegment completionSegment, System.EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}
