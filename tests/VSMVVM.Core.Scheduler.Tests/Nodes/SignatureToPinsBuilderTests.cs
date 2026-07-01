using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Nodes
{
    public class SignatureToPinsBuilderTests
    {
        // 시그니처 샘플들
        private static class Samples
        {
            public static int Add(int a, int b) => a + b;
            public static void Log(string message) { _ = message; }
            public static async Task DelayAsync(int ms) => await Task.Delay(ms).ConfigureAwait(false);
            public static async Task<string> EchoAsync(string s) { await Task.Yield(); return s; }
            public static async ValueTask<int> ValueAsync(int x) { await Task.Yield(); return x * 2; }
            public static int WithDefault(int a, int b = 10) => a + b;
            public static void RefParam(ref int x) { x++; }
        }

        private static MethodInfo M(string name) =>
            typeof(Samples).GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

        [Fact]
        public void IntAddIntInt_ProducesTwoIntInputsAndIntResult()
        {
            var pins = SignatureToPinsBuilder.Build(M(nameof(Samples.Add)));

            // 순서: In, a, b, Then, Result
            pins.Should().HaveCount(5);
            pins[0].Id.Should().Be("In");
            pins[0].Kind.Should().Be(PinKind.Exec);
            pins[0].Direction.Should().Be(PinDirection.Input);

            pins[1].Id.Should().Be("a");
            pins[1].ValueType.Should().Be(typeof(int));
            pins[1].Direction.Should().Be(PinDirection.Input);

            pins[2].Id.Should().Be("b");
            pins[2].ValueType.Should().Be(typeof(int));

            pins[3].Id.Should().Be("Then");
            pins[3].Kind.Should().Be(PinKind.Exec);
            pins[3].Direction.Should().Be(PinDirection.Output);

            pins[4].Id.Should().Be("Result");
            pins[4].ValueType.Should().Be(typeof(int));
            pins[4].Direction.Should().Be(PinDirection.Output);
        }

        [Fact]
        public void VoidMethod_OmitsResultPin()
        {
            var pins = SignatureToPinsBuilder.Build(M(nameof(Samples.Log)));

            // In, message, Then
            pins.Should().HaveCount(3);
            pins[^1].Id.Should().Be("Then");
        }

        [Fact]
        public void TaskMethod_TreatedAsVoid_NoResultPin()
        {
            var pins = SignatureToPinsBuilder.Build(M(nameof(Samples.DelayAsync)));

            pins.Should().HaveCount(3); // In, ms, Then
            pins[^1].Id.Should().Be("Then");
        }

        [Fact]
        public void TaskOfT_UnwrapsToT_ResultPin()
        {
            var pins = SignatureToPinsBuilder.Build(M(nameof(Samples.EchoAsync)));

            pins.Should().HaveCount(4); // In, s, Then, Result
            pins[^1].Id.Should().Be("Result");
            pins[^1].ValueType.Should().Be(typeof(string));
        }

        [Fact]
        public void ValueTaskOfT_UnwrapsToT_ResultPin()
        {
            var pins = SignatureToPinsBuilder.Build(M(nameof(Samples.ValueAsync)));

            pins.Should().HaveCount(4);
            pins[^1].Id.Should().Be("Result");
            pins[^1].ValueType.Should().Be(typeof(int));
        }

        [Fact]
        public void DefaultParameterValue_PropagatedAsPinDefault()
        {
            var pins = SignatureToPinsBuilder.Build(M(nameof(Samples.WithDefault)));

            var bPin = pins[2];
            bPin.Id.Should().Be("b");
            bPin.DefaultValue.Should().Be(10);
        }

        [Fact]
        public void RefParameter_Throws_NotSupportedException()
        {
            Action act = () => SignatureToPinsBuilder.Build(M(nameof(Samples.RefParam)));
            act.Should().Throw<NotSupportedException>()
               .WithMessage("*ref/out/in*");
        }

        [Fact]
        public void GetEffectiveReturnType_HandlesAllShapes()
        {
            SignatureToPinsBuilder.GetEffectiveReturnType(M(nameof(Samples.Add))).Should().Be(typeof(int));
            SignatureToPinsBuilder.GetEffectiveReturnType(M(nameof(Samples.Log))).Should().Be(typeof(void));
            SignatureToPinsBuilder.GetEffectiveReturnType(M(nameof(Samples.DelayAsync))).Should().Be(typeof(void));
            SignatureToPinsBuilder.GetEffectiveReturnType(M(nameof(Samples.EchoAsync))).Should().Be(typeof(string));
            SignatureToPinsBuilder.GetEffectiveReturnType(M(nameof(Samples.ValueAsync))).Should().Be(typeof(int));
        }
    }
}
