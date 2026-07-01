using System.Collections.Generic;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Pins;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Pins
{
    /// <summary>
    /// 묶음 A.4 — StableName 이 registry 의 GetByStableName + 자동 generic 인스턴스화와 함께
    /// 라운드트립(직렬화→역직렬화) 가능한지.
    /// </summary>
    public class PinTypeStableNameRoundTripTests
    {
        [Fact]
        public void Primitive_StableName_RoundTrip()
        {
            var registry = new DefaultPinTypeRegistry();
            var info = registry.Get(typeof(int));
            var roundTripped = registry.GetByStableName(info.StableName);
            roundTripped.Should().BeSameAs(info);
        }

        [Fact]
        public void ClosedGeneric_StableName_RoundTrip_ViaGetOrCreate()
        {
            var registry = new DefaultPinTypeRegistry();
            var info = registry.GetOrCreate(typeof(List<int>));
            info.Should().NotBeNull();

            // 라운드트립 — stable name 으로 다시 조회.
            var roundTripped = registry.GetByStableName(info.StableName);
            roundTripped.Should().BeSameAs(info);
        }

        [Fact]
        public void Array_StableName_RoundTrip_ViaGetOrCreate()
        {
            var registry = new DefaultPinTypeRegistry();
            var info = registry.GetOrCreate(typeof(double[]));
            info.Should().NotBeNull();
            info.StableName.Should().Be("System.Double[]");

            var roundTripped = registry.GetByStableName(info.StableName);
            roundTripped.Should().BeSameAs(info);
        }

        [Fact]
        public void Nested_StableName_RoundTrip()
        {
            var registry = new DefaultPinTypeRegistry();
            var info = registry.GetOrCreate(typeof(Dictionary<string, List<int>>));
            info.Should().NotBeNull();

            // 라운드트립.
            var roundTripped = registry.GetByStableName(info.StableName);
            roundTripped.Should().BeSameAs(info);
        }
    }
}
