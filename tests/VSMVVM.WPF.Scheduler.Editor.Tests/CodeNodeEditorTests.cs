using FluentAssertions;
using VSMVVM.WPF.Scheduler.Editor.Controls;
using VSMVVM.WPF.Scheduler.Editor.Themes;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Editor.Tests
{
    public class CodeNodeEditorTests
    {
        [StaFact]
        public void SourceCode_AppliedToEditor_OnDpSet()
        {
            var editor = new CodeNodeEditor();

            editor.SourceCode = "var x = 1;";

            editor.InnerEditor.Text.Should().Be("var x = 1;");
        }

        [StaFact]
        public void EditorTextChange_PropagatesBackToSourceCodeDp()
        {
            var editor = new CodeNodeEditor();
            editor.SourceCode = "before";

            editor.InnerEditor.Text = "after";

            editor.SourceCode.Should().Be("after");
        }

        [StaFact]
        public void Theme_DefaultIsDark_LoadsHighlighting()
        {
            var editor = new CodeNodeEditor();

            editor.Theme.Should().Be(EditorTheme.Dark);
            editor.InnerEditor.SyntaxHighlighting.Should().NotBeNull();
            editor.InnerEditor.SyntaxHighlighting.Name.Should().Be("C#");
        }

        [StaFact]
        public void Theme_Change_ToLight_AppliesDifferentHighlighting()
        {
            var editor = new CodeNodeEditor();
            var dark = editor.InnerEditor.SyntaxHighlighting;

            editor.Theme = EditorTheme.Light;

            editor.InnerEditor.SyntaxHighlighting.Should().NotBeSameAs(dark);
        }

        [StaFact]
        public void IsReadOnly_TrueLocksInnerEditor()
        {
            var editor = new CodeNodeEditor();
            editor.IsReadOnly = true;
            editor.InnerEditor.IsReadOnly.Should().BeTrue();
        }

        [StaFact]
        public void Diagnostics_DefaultIsEmpty_NotNull()
        {
            var editor = new CodeNodeEditor();
            editor.Diagnostics.Should().NotBeNull().And.BeEmpty();
        }

        [StaFact]
        public void EnableCompletion_DefaultIsTrue()
        {
            var editor = new CodeNodeEditor();
            editor.EnableCompletion.Should().BeTrue();
        }

        [StaFact]
        public void EnableCompletion_CanBeDisabled()
        {
            var editor = new CodeNodeEditor();
            editor.EnableCompletion = false;
            editor.EnableCompletion.Should().BeFalse();
        }
    }
}
