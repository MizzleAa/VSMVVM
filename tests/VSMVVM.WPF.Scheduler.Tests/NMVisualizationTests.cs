using System.Linq;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests
{
    /// <summary>
    /// Phase 11: N:M 시각화 — 같은 소스 핀에서 나가는 형제 연결들이 SiblingIndex/Count로 식별되고
    /// CurvatureOffset이 부채꼴로 분리된 값을 산출하는지 검증.
    /// </summary>
    public class NMVisualizationTests
    {
        private static (NodeGraphViewModel vm, NodeGraph graph) Build()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            return (new NodeGraphViewModel(g), g);
        }

        [Fact]
        public void SingleConnection_HasZeroCurvatureOffset()
        {
            var (vm, graph) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            graph.Connect(start.Id, "Then", end.Id, "In");

            var cvm = vm.Connections.Single();
            cvm.SiblingCount.Should().Be(1);
            cvm.SiblingIndex.Should().Be(0);
            cvm.CurvatureOffset.Should().Be(0);
        }

        [Fact]
        public void TwoSiblings_SameSourcePin_GetOppositeOffsets()
        {
            // start.Then → endA, endB — 두 형제 연결이 ±step/2 으로 분리
            var (vm, graph) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var endA = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            var endB = graph.AddNode(EndNode.TypeIdConst, 0, 0);

            graph.Connect(start.Id, "Then", endA.Id, "In");
            graph.Connect(start.Id, "Then", endB.Id, "In");

            vm.Connections.Should().HaveCount(2);
            foreach (var c in vm.Connections)
            {
                c.SiblingCount.Should().Be(2);
            }
            // step=12, center=0.5 → indices [0,1] → offsets [-6, +6]
            vm.Connections[0].CurvatureOffset.Should().Be(-6);
            vm.Connections[1].CurvatureOffset.Should().Be(+6);
        }

        [Fact]
        public void ThreeSiblings_GetSymmetricOffsets_AroundCenter()
        {
            var (vm, graph) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var e1 = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            var e2 = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            var e3 = graph.AddNode(EndNode.TypeIdConst, 0, 0);

            graph.Connect(start.Id, "Then", e1.Id, "In");
            graph.Connect(start.Id, "Then", e2.Id, "In");
            graph.Connect(start.Id, "Then", e3.Id, "In");

            // step=12, center=1 → indices [0,1,2] → offsets [-12, 0, +12]
            vm.Connections.Should().HaveCount(3);
            vm.Connections[0].CurvatureOffset.Should().Be(-12);
            vm.Connections[1].CurvatureOffset.Should().Be(0);
            vm.Connections[2].CurvatureOffset.Should().Be(+12);
        }

        [Fact]
        public void DifferentSourcePins_DoNotShareSiblings()
        {
            // Branch는 True/False 두 exec-out 핀이 별개 — 두 연결은 형제가 아니다.
            var (vm, graph) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var branch = graph.AddNode(BranchNode.TypeIdConst, 0, 0);
            var endTrue = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            var endFalse = graph.AddNode(EndNode.TypeIdConst, 0, 0);

            graph.Connect(start.Id, "Then", branch.Id, "In");
            graph.Connect(branch.Id, "True", endTrue.Id, "In");
            graph.Connect(branch.Id, "False", endFalse.Id, "In");

            // 3개 연결, 모두 서로 다른 소스 핀 → 각각 단독 형제
            foreach (var c in vm.Connections)
            {
                c.SiblingCount.Should().Be(1);
                c.CurvatureOffset.Should().Be(0);
            }
        }

        [Fact]
        public void Disconnect_OneSibling_ReorganizesRemaining()
        {
            // 형제 3개 중 하나 삭제 → 나머지 2개가 ±6으로 재정렬
            var (vm, graph) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var e1 = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            var e2 = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            var e3 = graph.AddNode(EndNode.TypeIdConst, 0, 0);

            var c1 = graph.Connect(start.Id, "Then", e1.Id, "In");
            graph.Connect(start.Id, "Then", e2.Id, "In");
            graph.Connect(start.Id, "Then", e3.Id, "In");

            graph.Disconnect(c1.Id); // 중간 형제 하나 제거

            vm.Connections.Should().HaveCount(2);
            foreach (var c in vm.Connections)
            {
                c.SiblingCount.Should().Be(2);
            }
            vm.Connections[0].CurvatureOffset.Should().Be(-6);
            vm.Connections[1].CurvatureOffset.Should().Be(+6);
        }
    }
}
