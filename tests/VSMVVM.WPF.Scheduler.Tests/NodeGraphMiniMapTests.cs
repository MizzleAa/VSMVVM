using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.WPF.Scheduler.Controls;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests
{
    public class NodeGraphMiniMapTests
    {
        [StaFact]
        public void RefreshFromGraph_ComputesContentBounds_FromNodes_WithPadding()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            graph.AddNode(StartNode.TypeIdConst, 100, 200);
            graph.AddNode(EndNode.TypeIdConst, 500, 600);

            var vm = new NodeGraphViewModel(graph);
            var map = new NodeGraphMiniMap { Graph = vm };
            map.RefreshFromGraph();

            // X range: 100 → 500 + 160 (NodeApproxWidth) = 660 → bounds (100, 200) - (660, 680)
            // 패딩 10%: bound 차이가 560/480 → padX=56, padY=48
            map.ContentOriginX.Should().BeApproximately(100 - 56, 0.01);
            map.ContentOriginY.Should().BeApproximately(200 - 48, 0.01);
            map.ComputedContentWidth.Should().BeApproximately(560 + 112, 0.01);
            map.ComputedContentHeight.Should().BeApproximately(480 + 96, 0.01);
        }

        [StaFact]
        public void RefreshFromGraph_EmptyGraph_ZeroBounds()
        {
            var graph = new NodeGraph();
            var vm = new NodeGraphViewModel(graph);
            var map = new NodeGraphMiniMap { Graph = vm };
            map.RefreshFromGraph();

            map.ComputedContentWidth.Should().Be(0);
            map.ComputedContentHeight.Should().Be(0);
        }
    }
}
