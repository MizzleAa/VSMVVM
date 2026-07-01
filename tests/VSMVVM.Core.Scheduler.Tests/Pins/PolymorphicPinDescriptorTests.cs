using System;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Pins
{
    /// <summary>
    /// Phase K — 다형성 핀. PinDescriptor 에 TypeParameterName 도입:
    ///   • null/empty 이면 정적 핀 (기존 동작).
    ///   • "T", "TKey" 등 식별자면 다형성 placeholder — ValueType 은 typeof(object) (런타임 substitution 전).
    ///   • NodeBase.BuildPins 가 IPolymorphicNode 의 TypeArguments 로 실제 타입 치환.
    /// </summary>
    public class PolymorphicPinDescriptorTests
    {
        [Fact]
        public void StaticPin_TypeParameterName_IsNull()
        {
            var d = new PinDescriptor("Value", "Value", PinDirection.Output, PinKind.Data, typeof(int), 0);
            d.TypeParameterName.Should().BeNull();
            d.IsPolymorphic.Should().BeFalse();
        }

        [Fact]
        public void PolymorphicPin_HoldsParameterName_AndObjectPlaceholder()
        {
            var d = new PinDescriptor("Item", "Item", PinDirection.Input, PinKind.Data,
                                      typeof(object), null, typeParameterName: "T");
            d.TypeParameterName.Should().Be("T");
            d.IsPolymorphic.Should().BeTrue();
            d.ValueType.Should().Be(typeof(object));
        }

        [Fact]
        public void Polymorphic_RejectsNonObjectValueType()
        {
            // 다형성 핀은 placeholder 라 typeof(object) 만 허용. 다른 정적 타입과 섞이면 의미 모순.
            Action act = () => new PinDescriptor("Item", "Item", PinDirection.Input, PinKind.Data,
                                                 typeof(int), null, typeParameterName: "T");
            act.Should().Throw<ArgumentException>().WithMessage("*polymorphic*");
        }

        [Fact]
        public void Polymorphic_ExecKind_NotAllowed()
        {
            // Exec 핀은 데이터를 운반하지 않으므로 다형성 의미 없음.
            Action act = () => new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec,
                                                 typeof(void), null, typeParameterName: "T");
            act.Should().Throw<ArgumentException>().WithMessage("*Exec*");
        }
    }
}
