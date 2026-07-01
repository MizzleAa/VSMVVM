using System.Linq;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Compilation;
using VSMVVM.WPF.Scheduler.Editor.Controls;
using VSMVVM.WPF.Scheduler.Editor.Renderers;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Editor.Tests
{
    public class RenderersTests
    {
        [StaFact]
        public void DiagnosticUnderlineRenderer_RegisteredOnInstall_OnSelectionLayer()
        {
            var editor = new CodeNodeEditor();
            var layers = editor.InnerEditor.TextArea.TextView.BackgroundRenderers;
            layers.Should().Contain(r => r is DiagnosticUnderlineRenderer);
            var d = layers.OfType<DiagnosticUnderlineRenderer>().First();
            d.Layer.Should().Be(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection);
        }

        [StaFact]
        public void CurrentLineHighlighter_RegisteredOnInstall_OnBackgroundLayer()
        {
            var editor = new CodeNodeEditor();
            var layers = editor.InnerEditor.TextArea.TextView.BackgroundRenderers;
            layers.Should().Contain(r => r is CurrentLineHighlighter);
            var h = layers.OfType<CurrentLineHighlighter>().First();
            h.Layer.Should().Be(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Background);
        }

        [StaFact]
        public void BreakpointMargin_AddedToLeftMargins_ByDefault()
        {
            var editor = new CodeNodeEditor();
            editor.InnerEditor.TextArea.LeftMargins.Should().Contain(m => m is BreakpointMargin);
        }

        [StaFact]
        public void ShowBreakpointMargin_False_RemovesMargin()
        {
            var editor = new CodeNodeEditor();
            editor.ShowBreakpointMargin = false;
            editor.InnerEditor.TextArea.LeftMargins.Should().NotContain(m => m is BreakpointMargin);
        }

        [StaFact]
        public void BreakpointsDp_ChangesPropagateToMargin()
        {
            var editor = new CodeNodeEditor();
            editor.Breakpoints = new[] { 1, 3, 5 };

            var margin = editor.InnerEditor.TextArea.LeftMargins
                .OfType<BreakpointMargin>().Single();
            margin.Breakpoints.Should().BeEquivalentTo(new[] { 1, 3, 5 });
        }

        [StaFact]
        public void MarginToggle_UpdatesBreakpointsDp_AndRaisesEvent()
        {
            var editor = new CodeNodeEditor();
            var margin = editor.InnerEditor.TextArea.LeftMargins.OfType<BreakpointMargin>().Single();

            BreakpointToggledEventArgs lastArgs = null;
            editor.BreakpointToggled += (_, e) => lastArgs = e;

            margin.ToggleBreakpoint(7);
            margin.ToggleBreakpoint(11);
            margin.ToggleBreakpoint(7); // 해제

            editor.Breakpoints.Should().BeEquivalentTo(new[] { 11 });
            lastArgs.Should().NotBeNull();
            lastArgs.LineNumber.Should().Be(7);
            lastArgs.IsSet.Should().BeFalse();
        }

        [StaFact]
        public void Diagnostics_Setter_DoesNotThrow_OnEmpty()
        {
            var editor = new CodeNodeEditor();
            // 빈 컬렉션 설정 → 어도너 invalidation은 안전해야 함.
            var act = () => editor.Diagnostics = System.Array.Empty<CompilationDiagnostic>();
            act.Should().NotThrow();
        }

        [StaFact]
        public void Diagnostics_Setter_WithError_DoesNotThrow()
        {
            var editor = new CodeNodeEditor();
            editor.SourceCode = "line1\nline2";
            var diag = new CompilationDiagnostic("CS0001",
                CompilationDiagnosticSeverity.Error,
                "test error",
                startLine: 1, startColumn: 1,
                endLine: 1, endColumn: 5);

            var act = () => editor.Diagnostics = new[] { diag };
            act.Should().NotThrow();
            editor.Diagnostics.Should().ContainSingle();
        }

        [Fact]
        public void DiagnosticUnderlineRenderer_NullDiagnosticsProvider_Throws()
        {
            var act = () => new DiagnosticUnderlineRenderer(null);
            act.Should().Throw<System.ArgumentNullException>();
        }

        [StaFact]
        public void BreakpointMargin_SetBreakpoints_NullClearsAll()
        {
            var m = new BreakpointMargin();
            m.SetBreakpoints(new[] { 1, 2, 3 });
            m.Breakpoints.Should().HaveCount(3);

            m.SetBreakpoints(null);
            m.Breakpoints.Should().BeEmpty();
        }
    }
}
