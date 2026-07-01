using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Runtime
{
    public class BuiltInNodesTests
    {
        [Fact]
        public void EnsureRegistered_RegistersAllSixBuiltIns_OnFirstCall()
        {
            BuiltInNodes.EnsureRegistered();

            NodeMetadataRegistry.Get(StartNode.TypeIdConst).Should().NotBeNull();
            NodeMetadataRegistry.Get(EndNode.TypeIdConst).Should().NotBeNull();
            NodeMetadataRegistry.Get(SequenceNode.TypeIdConst).Should().NotBeNull();
            NodeMetadataRegistry.Get(BranchNode.TypeIdConst).Should().NotBeNull();
            NodeMetadataRegistry.Get(DelayNode.TypeIdConst).Should().NotBeNull();
            NodeMetadataRegistry.Get(LogNode.TypeIdConst).Should().NotBeNull();
        }

        [Fact]
        public void EnsureRegistered_IsIdempotent()
        {
            BuiltInNodes.EnsureRegistered();
            BuiltInNodes.EnsureRegistered();
            BuiltInNodes.EnsureRegistered();
            // 중복 등록 시도가 있었으면 InvalidOperationException이 났을 것.
            NodeMetadataRegistry.Get(StartNode.TypeIdConst).Should().NotBeNull();
        }

        [Fact]
        public void BranchNode_PinsContainExecInDataCondExecTrueFalse()
        {
            var node = new BranchNode();
            node.Pins.Should().HaveCount(4);
            node.Pins[0].Id.Should().Be("In");
            node.Pins[1].Id.Should().Be("Condition");
            node.Pins[2].Id.Should().Be("True");
            node.Pins[3].Id.Should().Be("False");
        }
    }
}
