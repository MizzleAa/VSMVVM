using System.Reflection;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Nodes
{
    /// <summary>
    /// 묶음 B.5 — 매개변수 0개 + 반환값 있는 메소드는 "Constant-like" 모드로 핀을 빌드한다.
    /// 즉 exec 핀 없이 데이터 출력 1개만.
    /// </summary>
    public class SignatureToPinsBuilderConstantTests
    {
        private static int Pi() => 314;
        private static string Name() => "vsmvvm";

        [Fact]
        public void BuildAsConstant_ParameterlessMethod_NoExecPins_OnlyDataOut()
        {
            var method = typeof(SignatureToPinsBuilderConstantTests).GetMethod(
                nameof(Pi), BindingFlags.NonPublic | BindingFlags.Static);

            var pins = SignatureToPinsBuilder.BuildAsConstant(method);

            pins.Should().HaveCount(1, "Constant-like 모드는 데이터 출력 1개만");
            pins[0].Direction.Should().Be(PinDirection.Output);
            pins[0].Kind.Should().Be(PinKind.Data);
            pins[0].ValueType.Should().Be(typeof(int));
            pins[0].Id.Should().Be("Out");
        }

        [Fact]
        public void BuildAsConstant_OnVoidReturn_Throws()
        {
            // void 반환은 Constant 의미 없음.
            var method = typeof(SignatureToPinsBuilderConstantTests).GetMethod(
                nameof(DoNothing), BindingFlags.NonPublic | BindingFlags.Static);

            System.Action act = () => SignatureToPinsBuilder.BuildAsConstant(method);
            act.Should().Throw<System.NotSupportedException>()
                .WithMessage("*void*");
        }

        private static void DoNothing() { }

        [Fact]
        public void BuildAsConstant_WithParameters_Throws()
        {
            // 매개변수 있는 메소드는 Function 모드로 빌드해야 함.
            var method = typeof(SignatureToPinsBuilderConstantTests).GetMethod(
                nameof(Add), BindingFlags.NonPublic | BindingFlags.Static);

            System.Action act = () => SignatureToPinsBuilder.BuildAsConstant(method);
            act.Should().Throw<System.NotSupportedException>()
                .WithMessage("*parameters*");
        }

        private static int Add(int a, int b) => a + b;

        [Fact]
        public void IsConstantLike_True_ForParameterlessNonVoid()
        {
            var pi = typeof(SignatureToPinsBuilderConstantTests).GetMethod(
                nameof(Pi), BindingFlags.NonPublic | BindingFlags.Static);
            SignatureToPinsBuilder.IsConstantLike(pi).Should().BeTrue();
        }

        [Fact]
        public void IsConstantLike_False_ForVoidOrWithParameters()
        {
            var none = typeof(SignatureToPinsBuilderConstantTests).GetMethod(
                nameof(DoNothing), BindingFlags.NonPublic | BindingFlags.Static);
            var add = typeof(SignatureToPinsBuilderConstantTests).GetMethod(
                nameof(Add), BindingFlags.NonPublic | BindingFlags.Static);

            SignatureToPinsBuilder.IsConstantLike(none).Should().BeFalse();
            SignatureToPinsBuilder.IsConstantLike(add).Should().BeFalse();
        }

        private static (string Name, int Score, bool IsWinner) GetParams() => ("hero", 100, true);

        [Fact]
        public void BuildAsConstant_NamedValueTuple_ProducesPinPerElement()
        {
            var method = typeof(SignatureToPinsBuilderConstantTests).GetMethod(
                nameof(GetParams), BindingFlags.NonPublic | BindingFlags.Static);

            var pins = SignatureToPinsBuilder.BuildAsConstant(method);

            pins.Should().HaveCount(3);
            pins[0].Id.Should().Be("Name");
            pins[0].ValueType.Should().Be(typeof(string));
            pins[1].Id.Should().Be("Score");
            pins[1].ValueType.Should().Be(typeof(int));
            pins[2].Id.Should().Be("IsWinner");
            pins[2].ValueType.Should().Be(typeof(bool));
            pins.Should().OnlyContain(p => p.Direction == PinDirection.Output && p.Kind == PinKind.Data);
        }
    }
}
