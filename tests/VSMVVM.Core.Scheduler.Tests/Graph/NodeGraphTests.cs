using System.Collections.Generic;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Tests.TestHelpers;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Graph
{
    public class NodeGraphTests
    {
        [Fact]
        public void AddNode_AddsNodeAndLayout()
        {
            var graph = new NodeGraph();
            var node = new TestNode("Test.Start", Pin.ExecOut("Then"));

            graph.AddNode(node, 10, 20);

            graph.Nodes.Should().HaveCount(1);
            graph.GetNode(node.Id).Should().BeSameAs(node);
            graph.Layouts[node.Id].X.Should().Be(10);
            graph.Layouts[node.Id].Y.Should().Be(20);
        }

        [Fact]
        public void RemoveNode_AlsoRemovesAttachedConnections()
        {
            var graph = new NodeGraph();
            var a = new TestNode("Test.A", Pin.ExecOut("Then"));
            var b = new TestNode("Test.B", Pin.ExecIn("In"));
            graph.AddNode(a, 0, 0);
            graph.AddNode(b, 100, 0);
            graph.Connect(a.Id, "Then", b.Id, "In");

            graph.Connections.Should().HaveCount(1);
            graph.RemoveNode(b.Id);

            graph.Nodes.Should().HaveCount(1);
            graph.Connections.Should().BeEmpty();
        }

        [Fact]
        public void Connect_IncompatiblePins_Throws()
        {
            var graph = new NodeGraph();
            var a = new TestNode("Test.A", Pin.DataOut<int>("Out"));
            var b = new TestNode("Test.B", Pin.DataIn<string>("In"));
            graph.AddNode(a, 0, 0);
            graph.AddNode(b, 0, 0);

            System.Action act = () => graph.Connect(a.Id, "Out", b.Id, "In");
            act.Should().Throw<System.InvalidOperationException>()
               .WithMessage("Cannot connect:*not assignable*");
        }

        [Fact]
        public void Connect_DataInputAlreadyConnected_AutoDisconnectsPrevious()
        {
            // N:M 규칙: data-input은 1:1, 새 연결이 들어오면 기존 연결 자동 제거.
            var graph = new NodeGraph();
            var srcA = new TestNode("Test.SrcA", Pin.DataOut<int>("Out"));
            var srcB = new TestNode("Test.SrcB", Pin.DataOut<int>("Out"));
            var dst = new TestNode("Test.Dst", Pin.DataIn<int>("In"));
            graph.AddNode(srcA, 0, 0);
            graph.AddNode(srcB, 0, 0);
            graph.AddNode(dst, 0, 0);

            var first = graph.Connect(srcA.Id, "Out", dst.Id, "In");
            var second = graph.Connect(srcB.Id, "Out", dst.Id, "In");

            graph.Connections.Should().HaveCount(1);
            graph.Connections[0].Id.Should().Be(second.Id);
            graph.Connections.Should().NotContain(c => c.Id == first.Id);
        }

        [Fact]
        public void Connect_MultipleExecOutsFromSameSource_AllPersisted()
        {
            // N:M 규칙: exec-output은 1:N (한 핀에서 여러 노드로 분기).
            var graph = new NodeGraph();
            var src = new TestNode("Test.Src", Pin.ExecOut("Then"));
            var dstA = new TestNode("Test.DstA", Pin.ExecIn("In"));
            var dstB = new TestNode("Test.DstB", Pin.ExecIn("In"));
            var dstC = new TestNode("Test.DstC", Pin.ExecIn("In"));
            graph.AddNode(src, 0, 0);
            graph.AddNode(dstA, 0, 0);
            graph.AddNode(dstB, 0, 0);
            graph.AddNode(dstC, 0, 0);

            graph.Connect(src.Id, "Then", dstA.Id, "In");
            graph.Connect(src.Id, "Then", dstB.Id, "In");
            graph.Connect(src.Id, "Then", dstC.Id, "In");

            graph.Connections.Should().HaveCount(3);
        }

        [Fact]
        public void Connect_MultipleSourcesIntoExecInput_NMConvergencePoint_Persisted()
        {
            // N:M 규칙: exec-input은 N:1 (여러 곳에서 합류).
            var graph = new NodeGraph();
            var a = new TestNode("Test.A", Pin.ExecOut("Then"));
            var b = new TestNode("Test.B", Pin.ExecOut("Then"));
            var converge = new TestNode("Test.Conv", Pin.ExecIn("In"));
            graph.AddNode(a, 0, 0);
            graph.AddNode(b, 0, 0);
            graph.AddNode(converge, 0, 0);

            graph.Connect(a.Id, "Then", converge.Id, "In");
            graph.Connect(b.Id, "Then", converge.Id, "In");

            graph.Connections.Should().HaveCount(2);
        }

        [Fact]
        public void Disconnect_ExistingConnection_ReturnsTrue_AndRemoves()
        {
            var graph = new NodeGraph();
            var a = new TestNode("Test.A", Pin.ExecOut("Then"));
            var b = new TestNode("Test.B", Pin.ExecIn("In"));
            graph.AddNode(a, 0, 0);
            graph.AddNode(b, 0, 0);
            var c = graph.Connect(a.Id, "Then", b.Id, "In");

            var removed = graph.Disconnect(c.Id);

            removed.Should().BeTrue();
            graph.Connections.Should().BeEmpty();
        }

        [Fact]
        public void MoveNode_UpdatesLayout_AndRaisesMovedEvent()
        {
            var graph = new NodeGraph();
            var a = new TestNode("Test.A");
            graph.AddNode(a, 10, 20);

            var changes = new List<GraphChange>();
            graph.Changed += (g, ch) => changes.Add(ch);

            graph.MoveNode(a.Id, 30, 40);

            graph.Layouts[a.Id].X.Should().Be(30);
            graph.Layouts[a.Id].Y.Should().Be(40);

            changes.Should().ContainSingle(c => c is Moved)
                .Which.Should().BeOfType<Moved>()
                .Which.To.X.Should().Be(30);
        }

        [Fact]
        public void Changed_Event_FiresOnAddRemoveConnect()
        {
            var graph = new NodeGraph();
            var a = new TestNode("Test.A", Pin.ExecOut("Then"));
            var b = new TestNode("Test.B", Pin.ExecIn("In"));

            var changes = new List<GraphChange>();
            graph.Changed += (g, ch) => changes.Add(ch);

            graph.AddNode(a, 0, 0);
            graph.AddNode(b, 0, 0);
            var c = graph.Connect(a.Id, "Then", b.Id, "In");
            graph.Disconnect(c.Id);
            graph.RemoveNode(b.Id);

            changes.Should().HaveCount(5);
            changes[0].Should().BeOfType<NodeAdded>();
            changes[1].Should().BeOfType<NodeAdded>();
            changes[2].Should().BeOfType<Connected>();
            changes[3].Should().BeOfType<Disconnected>();
            changes[4].Should().BeOfType<NodeRemoved>();
        }
    }
}
