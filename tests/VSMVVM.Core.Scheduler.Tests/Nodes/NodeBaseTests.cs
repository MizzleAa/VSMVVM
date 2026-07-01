using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Tests.TestHelpers;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Nodes
{
    public class NodeBaseTests
    {
        [Fact]
        public void Pins_AreBuiltFromDescriptors_WithCorrectOwnerAndType()
        {
            var node = new TestNode("Test.N",
                Pin.ExecIn("In"),
                Pin.DataIn<int>("Count", 42),
                Pin.DataOut<string>("Result"),
                Pin.ExecOut("Then"));

            node.Pins.Should().HaveCount(4);
            node.Pins[0].Should().BeOfType<ExecPin>();
            node.Pins[0].Direction.Should().Be(PinDirection.Input);
            node.Pins[0].Owner.Should().BeSameAs(node);

            node.Pins[1].Kind.Should().Be(PinKind.Data);
            node.Pins[1].ValueType.Should().Be(typeof(int));
            ((DataPin)node.Pins[1]).DefaultValue.Should().Be(42);

            node.Pins[2].Kind.Should().Be(PinKind.Data);
            node.Pins[2].ValueType.Should().Be(typeof(string));

            node.Pins[3].Should().BeOfType<ExecPin>();
            node.Pins[3].Direction.Should().Be(PinDirection.Output);
        }

        [Fact]
        public void NodeId_IsUniquePerInstance()
        {
            var a = new TestNode("Test.N");
            var b = new TestNode("Test.N");

            a.Id.Should().NotBe(b.Id);
        }
    }

    public class ExecutionFlowTests
    {
        [Fact]
        public void Halt_HasNoFiredPins()
        {
            ExecutionFlow.Halt.IsHalt.Should().BeTrue();
            ExecutionFlow.Halt.FiredPinIds.Should().BeEmpty();
        }

        [Fact]
        public void Continue_WithIds_PreservesOrder()
        {
            var f = ExecutionFlow.Continue("A", "B", "C");

            f.IsHalt.Should().BeFalse();
            f.FiredPinIds.Should().Equal("A", "B", "C");
        }

        [Fact]
        public void Continue_WithEmpty_ReducesToHalt()
        {
            var f = ExecutionFlow.Continue();

            f.IsHalt.Should().BeTrue();
        }

        [Fact]
        public void Equality_Works()
        {
            ExecutionFlow.Continue("A").Should().Be(ExecutionFlow.Continue("A"));
            ExecutionFlow.Continue("A").Should().NotBe(ExecutionFlow.Continue("B"));
            ExecutionFlow.Halt.Should().Be(ExecutionFlow.Halt);
        }
    }
}
