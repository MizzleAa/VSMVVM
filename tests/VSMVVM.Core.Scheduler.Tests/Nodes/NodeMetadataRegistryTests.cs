using System;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Tests.TestHelpers;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Nodes
{
    /// <summary>
    /// 글로벌 NodeMetadataRegistry에 영향을 주는 테스트들. 각 테스트는 자체적으로 고유한 TypeId를 사용하고
    /// UnregisterForTests로 정리하여 ModuleInitializer가 등록한 다른 노드(SmokeTestNode 등)에 영향을 주지 않도록 한다.
    /// </summary>
    [Xunit.Collection(nameof(NodeMetadataRegistryCollection))]
    public class NodeMetadataRegistryTests
    {
        private static NodeMetadata MakeMetadata(string typeId)
        {
            return new NodeMetadata(
                typeId, typeId, "TestCategory", "desc", 0,
                typeof(TestNode),
                () => new TestNode(typeId, Pin.ExecIn(), Pin.ExecOut()),
                new[] { Pin.ExecIn(), Pin.ExecOut() });
        }

        [Fact]
        public void Register_NewType_Succeeds_AndCanBeQueried()
        {
            const string typeId = "Test.Registry.Register_NewType";
            try
            {
                var meta = MakeMetadata(typeId);
                NodeMetadataRegistry.Register(meta);

                NodeMetadataRegistry.Get(typeId).Should().BeSameAs(meta);
                NodeMetadataRegistry.All.Should().Contain(meta);
            }
            finally { NodeMetadataRegistry.UnregisterForTests(typeId); }
        }

        [Fact]
        public void Register_DuplicateTypeId_Throws()
        {
            const string typeId = "Test.Registry.Duplicate";
            try
            {
                NodeMetadataRegistry.Register(MakeMetadata(typeId));

                Action act = () => NodeMetadataRegistry.Register(MakeMetadata(typeId));

                act.Should().Throw<InvalidOperationException>()
                   .WithMessage($"*'{typeId}' is already registered*");
            }
            finally { NodeMetadataRegistry.UnregisterForTests(typeId); }
        }

        [Fact]
        public void Get_UnknownTypeId_ReturnsNull()
        {
            NodeMetadataRegistry.Get("Nonexistent.Type.Id.Xyz").Should().BeNull();
        }

        [Fact]
        public void Factory_ProducesNewInstanceEachCall()
        {
            const string typeId = "Test.Registry.Factory";
            try
            {
                var meta = MakeMetadata(typeId);
                NodeMetadataRegistry.Register(meta);

                var a = meta.Factory();
                var b = meta.Factory();

                a.Should().NotBeSameAs(b);
                a.TypeId.Should().Be(typeId);
                b.TypeId.Should().Be(typeId);
            }
            finally { NodeMetadataRegistry.UnregisterForTests(typeId); }
        }
    }
}
