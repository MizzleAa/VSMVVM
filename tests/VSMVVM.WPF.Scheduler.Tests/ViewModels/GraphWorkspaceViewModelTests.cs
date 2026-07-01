using System;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Compilation;
using VSMVVM.WPF.Scheduler.Services;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;
using System.Collections.Generic;
using System.Reflection;

namespace VSMVVM.WPF.Scheduler.Tests.ViewModels
{
    /// <summary>
    /// Phase M 이후 GraphWorkspaceViewModel 은 그래프 + 변수만 담당. 사용자 코드 (CodeSnippets / CompileAllSnippets) 는
    /// SchedulerDemoViewModel (컨테이너) 로 이동. 컴파일 로직 단위 테스트는 컨테이너 테스트로 이전됨 —
    /// 본 파일은 워크스페이스의 빈 그래프 + DisplayName 만 검증.
    /// </summary>
    public sealed class GraphWorkspaceViewModelTests
    {
        private sealed class StubCompilationService : ICompilationService
        {
            public CompilationResult Compile(string sourceCode, CompilationOptions options)
                => throw new InvalidOperationException("Stub should not be called in workspace-level tests.");

            public IReadOnlyList<CompilationDiagnostic> Analyze(string sourceCode, CompilationOptions options)
                => Array.Empty<CompilationDiagnostic>();

            public bool UnloadAssembly(Assembly assembly) => true;
        }

        private static GraphWorkspaceViewModel CreateWorkspace()
            => new GraphWorkspaceViewModel(compiler: new StubCompilationService(), palette: new NodePaletteService());

        [Fact]
        public void New_workspace_starts_with_empty_graph()
        {
            var ws = CreateWorkspace();

            ws.GraphVm.Should().NotBeNull();
            ws.GraphVm.Nodes.Should().BeEmpty();
            ws.GraphVm.Connections.Should().BeEmpty();
        }

        [Fact]
        public void New_workspace_has_a_display_name()
        {
            var ws = CreateWorkspace();
            ws.DisplayName.Should().NotBeNullOrWhiteSpace();
        }
    }
}
