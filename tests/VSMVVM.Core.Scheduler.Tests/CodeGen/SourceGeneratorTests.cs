using System.Linq;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Tests.TestHelpers;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.CodeGen
{
    /// <summary>
    /// Phase 2 검증: SourceGenerator가 [Node] 클래스에 partial을 emit하고
    /// [ModuleInitializer]로 NodeMetadataRegistry에 자동 등록하는지 확인.
    /// SmokeTestNode (VSMVVM.Core.Scheduler.Tests 어셈블리)이 검증 대상 —
    /// generator는 자기 어셈블리에는 실행되지 않고 소비 어셈블리에서만 동작.
    /// </summary>
    public class SourceGeneratorTests
    {
        [Fact]
        public void SmokeTestNode_IsAutoRegistered_OnModuleLoad()
        {
            // ModuleInitializer가 실행되어 SmokeTestNode가 등록되었어야 한다.
            var meta = NodeMetadataRegistry.Get("Core.SmokeTest");

            meta.Should().NotBeNull();
            meta.DisplayName.Should().Be("Smoke Test");
            meta.Category.Should().Be("Diagnostics");
            meta.ClrType.Should().Be(typeof(SmokeTestNode));
        }

        [Fact]
        public void Factory_CreatesNewNodeInstance()
        {
            var meta = NodeMetadataRegistry.Get("Core.SmokeTest");

            var node = meta.Factory();

            node.Should().BeOfType<SmokeTestNode>();
            node.TypeId.Should().Be("Core.SmokeTest");
        }

        [Fact]
        public void PinsAreEmittedInDeclaredOrder_WithCorrectKindsAndTypes()
        {
            var meta = NodeMetadataRegistry.Get("Core.SmokeTest");

            meta.Pins.Should().HaveCount(4);

            meta.Pins[0].Id.Should().Be("In");
            meta.Pins[0].Kind.Should().Be(PinKind.Exec);
            meta.Pins[0].Direction.Should().Be(PinDirection.Input);

            meta.Pins[1].Id.Should().Be("Then");
            meta.Pins[1].Kind.Should().Be(PinKind.Exec);
            meta.Pins[1].Direction.Should().Be(PinDirection.Output);

            meta.Pins[2].Id.Should().Be("Count");
            meta.Pins[2].Kind.Should().Be(PinKind.Data);
            meta.Pins[2].Direction.Should().Be(PinDirection.Input);
            meta.Pins[2].ValueType.Should().Be(typeof(int));
            meta.Pins[2].DefaultValue.Should().Be(1);

            meta.Pins[3].Id.Should().Be("Message");
            meta.Pins[3].Kind.Should().Be(PinKind.Data);
            meta.Pins[3].Direction.Should().Be(PinDirection.Output);
            meta.Pins[3].ValueType.Should().Be(typeof(string));
        }

        [Fact]
        public void NodeInstance_PinsAreBuiltFromGeneratedDescriptors()
        {
            var node = new SmokeTestNode();

            node.Pins.Should().HaveCount(4);
            node.Pins.Select(p => p.Id).Should().Equal("In", "Then", "Count", "Message");

            // Data 입력 핀의 기본값은 DataPin<int>로 박싱돼야 한다.
            var countPin = node.Pins[2];
            countPin.Should().BeOfType<DataPin<int>>();
            ((DataPin<int>)countPin).DefaultValue.Should().Be(1);
        }

        [Fact]
        public void Registry_Contains_SmokeTestNode_InAllList()
        {
            NodeMetadataRegistry.All.Should().Contain(m => m.TypeId == "Core.SmokeTest");
        }
    }
}
