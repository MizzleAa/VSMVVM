using FluentAssertions;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Tests.TestHelpers;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Pins
{
    public class PinCompatibilityTests
    {
        [Fact]
        public void ExecToExec_OppositeDirection_Compatible()
        {
            var src = new TestNode("Test.Src", Pin.ExecOut("Then"));
            var dst = new TestNode("Test.Dst", Pin.ExecIn("In"));

            var ok = PinCompatibility.CanConnect(src.Pins[0], dst.Pins[0], out var reason);

            ok.Should().BeTrue();
            reason.Should().BeNull();
        }

        [Fact]
        public void ExecToData_DifferentKind_Incompatible()
        {
            var src = new TestNode("Test.Src", Pin.ExecOut("Then"));
            var dst = new TestNode("Test.Dst", Pin.DataIn<int>("Value"));

            var ok = PinCompatibility.CanConnect(src.Pins[0], dst.Pins[0], out var reason);

            ok.Should().BeFalse();
            reason.Should().Contain("Pin kinds do not match");
        }

        [Fact]
        public void OutputToOutput_SameDirection_NeverCompatible()
        {
            var a = new TestNode("Test.A", Pin.DataOut<int>("Out"));
            var b = new TestNode("Test.B", Pin.DataOut<int>("Out"));

            var ok = PinCompatibility.CanConnect(a.Pins[0], b.Pins[0], out var reason);

            ok.Should().BeFalse();
            reason.Should().Contain("Target pin must be an input pin");
        }

        [Fact]
        public void InputToInput_SameDirection_NeverCompatible()
        {
            var a = new TestNode("Test.A", Pin.DataIn<int>("In"));
            var b = new TestNode("Test.B", Pin.DataIn<int>("In"));

            var ok = PinCompatibility.CanConnect(a.Pins[0], b.Pins[0], out var reason);

            ok.Should().BeFalse();
            reason.Should().Contain("Source pin must be an output pin");
        }

        [Fact]
        public void DataT_ToDataT_SameType_Compatible()
        {
            var src = new TestNode("Test.Src", Pin.DataOut<string>("Out"));
            var dst = new TestNode("Test.Dst", Pin.DataIn<string>("In"));

            var ok = PinCompatibility.CanConnect(src.Pins[0], dst.Pins[0], out var reason);

            ok.Should().BeTrue();
            reason.Should().BeNull();
        }

        [Fact]
        public void DataInt_ToDataDouble_StrictByDefault_Incompatible()
        {
            // Phase 3a에서 IConvertible 위닝 converter 도입 전까지는 strict assignability만 적용.
            var src = new TestNode("Test.Src", Pin.DataOut<int>("Out"));
            var dst = new TestNode("Test.Dst", Pin.DataIn<double>("In"));

            var ok = PinCompatibility.CanConnect(src.Pins[0], dst.Pins[0], out var reason);

            ok.Should().BeFalse();
            reason.Should().Contain("not assignable");
        }

        [Fact]
        public void DataInt_ToDataObject_Compatible_ViaBoxing()
        {
            // Type.IsAssignableFrom은 value type → object의 박싱 변환을 허용한다.
            // 런타임은 박싱을 수행하므로 정상 호환으로 간주.
            var src = new TestNode("Test.Src", Pin.DataOut<int>("Out"));
            var dst = new TestNode("Test.Dst", Pin.DataIn<object>("In"));

            var ok = PinCompatibility.CanConnect(src.Pins[0], dst.Pins[0], out var reason);

            ok.Should().BeTrue();
            reason.Should().BeNull();
        }
    }
}
