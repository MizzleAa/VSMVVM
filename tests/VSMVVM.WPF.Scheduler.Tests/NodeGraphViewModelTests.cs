using System.Linq;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests
{
    public class NodeGraphViewModelTests
    {
        private static NodeGraph EmptyGraph() => new();

        [Fact]
        public void Construct_PrePopulatedGraph_BuildsViewModelsForEachNodeAndConnection()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = EmptyGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 10, 20);
            var end = graph.AddNode(EndNode.TypeIdConst, 100, 20);
            graph.Connect(start.Id, "Then", end.Id, "In");

            var vm = new NodeGraphViewModel(graph);

            vm.Nodes.Should().HaveCount(2);
            vm.Connections.Should().HaveCount(1);
            vm.FindNode(start.Id).TypeId.Should().Be(StartNode.TypeIdConst);
        }

        [Fact]
        public void GraphAddNode_PropagatesTo_NodesCollection()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = EmptyGraph();
            var vm = new NodeGraphViewModel(graph);

            var node = graph.AddNode(StartNode.TypeIdConst, 0, 0);

            vm.Nodes.Should().ContainSingle();
            vm.Nodes.Single().Id.Should().Be(node.Id);
        }

        [Fact]
        public void GraphRemoveNode_PropagatesTo_NodesCollection_AndClearsSelectedIfRemoved()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = EmptyGraph();
            var node = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var vm = new NodeGraphViewModel(graph);
            vm.SelectedNode = vm.Nodes[0];

            graph.RemoveNode(node.Id);

            vm.Nodes.Should().BeEmpty();
            vm.SelectedNode.Should().BeNull();
        }

        [Fact]
        public void GraphConnect_PropagatesTo_ConnectionsCollection()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = EmptyGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            var vm = new NodeGraphViewModel(graph);

            var conn = graph.Connect(start.Id, "Then", end.Id, "In");

            vm.Connections.Should().ContainSingle();
            vm.Connections.Single().Id.Should().Be(conn.Id);
        }

        [Fact]
        public void GraphDisconnect_PropagatesTo_ConnectionsCollection()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = EmptyGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            var conn = graph.Connect(start.Id, "Then", end.Id, "In");
            var vm = new NodeGraphViewModel(graph);

            graph.Disconnect(conn.Id);

            vm.Connections.Should().BeEmpty();
        }

        [Fact]
        public void GraphMoveNode_UpdatesNodeViewModelXY()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = EmptyGraph();
            var node = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var vm = new NodeGraphViewModel(graph);

            graph.MoveNode(node.Id, 123, 456);

            var nvm = vm.Nodes.Single();
            nvm.X.Should().Be(123);
            nvm.Y.Should().Be(456);
        }

        [Fact]
        public void NodeRemoveSelected_RemovesFromGraph()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = EmptyGraph();
            var node = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var vm = new NodeGraphViewModel(graph);
            vm.SelectedNode = vm.Nodes[0];

            vm.RemoveSelectedCommand.Execute(null);

            graph.Nodes.Should().BeEmpty();
        }
    }

    public class ConnectionViewModelTests
    {
        [Fact]
        public void Endpoints_RecomputeWhen_SourceNodeMoves()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 10, 10);
            var end = graph.AddNode(EndNode.TypeIdConst, 100, 50);
            graph.Connect(start.Id, "Then", end.Id, "In");

            var vm = new NodeGraphViewModel(graph);
            var cvm = vm.Connections.Single();
            cvm.SourcePinOffset = new System.Windows.Point(150, 30);
            cvm.TargetPinOffset = new System.Windows.Point(0, 30);

            var initialStart = cvm.Start;
            graph.MoveNode(start.Id, 200, 200);

            cvm.Start.Should().NotBe(initialStart);
            cvm.Start.X.Should().Be(200 + 150);
            cvm.Start.Y.Should().Be(200 + 30);
        }
    }

    public class PinViewModelTests
    {
        [Fact]
        public void LiteralValueChange_PropagatesTo_NodeLiteralInputs()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var branch = graph.AddNode(BranchNode.TypeIdConst, 0, 0);
            var vm = new NodeGraphViewModel(graph);

            var branchVm = vm.FindNode(branch.Id);
            var condPin = branchVm.InputPins.First(p => p.Id == "Condition");
            condPin.LiteralValue = true;

            // 노드 모델에 반영
            var nb = (VSMVVM.Core.Scheduler.Nodes.NodeBase)branch;
            nb.LiteralInputs.Should().ContainKey("Condition")
              .WhoseValue.Should().Be(true);
        }
    }
}
