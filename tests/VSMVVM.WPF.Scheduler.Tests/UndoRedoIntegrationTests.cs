using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.WPF.Scheduler.ViewModels;
using VSMVVM.WPF.Services;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests
{
    public class UndoRedoIntegrationTests
    {
        private static (NodeGraphViewModel vm, NodeGraph graph, IUndoRedoService undo) Build()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var undo = new UndoRedoService();
            var vm = new NodeGraphViewModel(graph, undo);
            return (vm, graph, undo);
        }

        [Fact]
        public void AddNode_ThenUndo_RemovesNode()
        {
            var (vm, graph, undo) = Build();

            graph.AddNode(StartNode.TypeIdConst, 100, 100);
            vm.Nodes.Should().ContainSingle();
            undo.CanUndo.Should().BeTrue();

            undo.Undo();

            vm.Nodes.Should().BeEmpty();
            graph.Nodes.Should().BeEmpty();
            undo.CanRedo.Should().BeTrue();
        }

        [Fact]
        public void AddNode_Undo_Redo_RestoresNode()
        {
            var (vm, graph, undo) = Build();

            var n = graph.AddNode(StartNode.TypeIdConst, 50, 50);
            undo.Undo();
            undo.Redo();

            vm.Nodes.Should().ContainSingle();
            graph.GetNode(n.Id).Should().NotBeNull();
        }

        [Fact]
        public void Connect_ThenUndo_RemovesConnection()
        {
            var (vm, graph, undo) = Build();
            var s = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var e = graph.AddNode(EndNode.TypeIdConst, 100, 0);

            graph.Connect(s.Id, "Then", e.Id, "In");
            vm.Connections.Should().ContainSingle();

            undo.Undo();

            vm.Connections.Should().BeEmpty();
        }

        [Fact]
        public void MoveNode_Undo_RestoresPreviousPosition()
        {
            var (vm, graph, undo) = Build();
            var n = graph.AddNode(StartNode.TypeIdConst, 10, 20);

            graph.MoveNode(n.Id, 200, 300);
            undo.Undo();

            graph.Layouts[n.Id].X.Should().Be(10);
            graph.Layouts[n.Id].Y.Should().Be(20);
        }

        [Fact]
        public void RemoveSelectedCommand_ThenUndo_RestoresNode_WithoutConnections()
        {
            // Phase 6 단순화: RemoveSelected가 Undo되면 노드 본체만 복원. 부산물 연결은 미복원 (Phase 7 강화 대상).
            var (vm, graph, undo) = Build();
            var n = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            vm.SelectedNode = vm.Nodes[0];

            vm.RemoveSelectedCommand.Execute(null);
            graph.Nodes.Should().BeEmpty();

            undo.Undo();
            graph.Nodes.Should().ContainSingle()
                .Which.Id.Should().Be(n.Id);
        }
    }
}
