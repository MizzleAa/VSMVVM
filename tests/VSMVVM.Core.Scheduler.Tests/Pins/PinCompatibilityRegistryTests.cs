using System.Collections.Generic;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Tests.TestHelpers;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Pins
{
    /// <summary>
    /// 묶음 A.3 — PinCompatibility 가 IPinTypeRegistry 와 연동되어 미등록 타입을 거부하는지.
    /// </summary>
    public class PinCompatibilityRegistryTests
    {
        private sealed class TestNode : VSMVVM.Core.Scheduler.Nodes.NodeBase
        {
            public override string TypeId => "Test.PinCompatRegistry";
            private readonly System.Collections.Generic.IReadOnlyList<VSMVVM.Core.Scheduler.Nodes.PinDescriptor> _pins;
            public TestNode(params VSMVVM.Core.Scheduler.Nodes.PinDescriptor[] pins) => _pins = pins;
            protected override System.Collections.Generic.IReadOnlyList<VSMVVM.Core.Scheduler.Nodes.PinDescriptor> GetPinDescriptors() => _pins;
        }

        private sealed class UnregisteredCustom { }

        private static (IPin src, IPin dst) MakePinPair(System.Type srcType, System.Type dstType)
        {
            var srcNode = new TestNode(
                new VSMVVM.Core.Scheduler.Nodes.PinDescriptor("Out", "Out", PinDirection.Output, PinKind.Data, srcType, null));
            var dstNode = new TestNode(
                new VSMVVM.Core.Scheduler.Nodes.PinDescriptor("In", "In", PinDirection.Input, PinKind.Data, dstType, null));
            return (srcNode.Pins[0], dstNode.Pins[0]);
        }

        [Fact]
        public void WithRegistry_BothTypesRegistered_StillRequiresIsAssignableFrom()
        {
            // 기본 동작 유지 — int → string 은 여전히 거부.
            var registry = new DefaultPinTypeRegistry();
            var (src, dst) = MakePinPair(typeof(int), typeof(string));

            PinCompatibility.CanConnect(src, dst, registry, out var reason).Should().BeFalse();
            reason.Should().Contain("not assignable");
        }

        [Fact]
        public void WithRegistry_BothTypesRegistered_SameTypeAllowed()
        {
            var registry = new DefaultPinTypeRegistry();
            var (src, dst) = MakePinPair(typeof(int), typeof(int));

            PinCompatibility.CanConnect(src, dst, registry, out _).Should().BeTrue();
        }

        [Fact]
        public void WithRegistry_UnregisteredType_RejectedWithReason()
        {
            var registry = new DefaultPinTypeRegistry();
            var (src, dst) = MakePinPair(typeof(UnregisteredCustom), typeof(UnregisteredCustom));

            PinCompatibility.CanConnect(src, dst, registry, out var reason).Should().BeFalse();
            reason.Should().Contain("not registered");
        }

        [Fact]
        public void WithRegistry_RegisteredClosedGeneric_AllowedAfterAutoCreate()
        {
            var registry = new DefaultPinTypeRegistry();
            // List<int> 는 명시 등록 안 됐지만 List<> + int 가 있으므로 GetOrCreate 로 자동 인스턴스화.
            registry.GetOrCreate(typeof(List<int>));
            var (src, dst) = MakePinPair(typeof(List<int>), typeof(List<int>));

            PinCompatibility.CanConnect(src, dst, registry, out _).Should().BeTrue();
        }

        [Fact]
        public void WithoutRegistry_FallbackToLegacyBehavior_AcceptsAnyAssignable()
        {
            // 기존 API 호환 — registry 인자 없는 오버로드는 그대로 동작.
            var (src, dst) = MakePinPair(typeof(int), typeof(int));
            PinCompatibility.CanConnect(src, dst, out _).Should().BeTrue();
        }
    }
}
