using System.Linq;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Compilation;
using VSMVVM.Core.Scheduler.Scripting;
using Xunit;

namespace VSMVVM.Core.Scheduler.Scripting.Tests
{
    public class RoslynCompilationServiceTests
    {
        private const string AddSource = @"
namespace UserCode
{
    public static class MyMath
    {
        public static int Add(int a, int b) => a + b;
    }
}";

        private const string BrokenSource = @"
namespace UserCode
{
    public static class MyMath
    {
        public static int Add(int a, int b => a + b;  // missing )
    }
}";

        [Fact]
        public void Compile_ValidIntAdd_ReturnsAssembly_AndMethodInvokesCorrectly()
        {
            var svc = new RoslynCompilationService();
            var options = new CompilationOptions { AssemblyName = "Compile_ValidIntAdd" };

            var result = svc.Compile(AddSource, options);

            result.Success.Should().BeTrue();
            result.Assembly.Should().NotBeNull();
            result.Diagnostics.Should().NotContain(d => d.Severity == CompilationDiagnosticSeverity.Error);

            var type = result.Assembly.GetType("UserCode.MyMath");
            type.Should().NotBeNull();
            var sum = type.GetMethod("Add").Invoke(null, new object[] { 17, 25 });
            sum.Should().Be(42);

            svc.UnloadAssembly(result.Assembly).Should().BeTrue();
        }

        [Fact]
        public void Compile_SyntaxError_ReturnsFailedResult_WithDiagnostics()
        {
            var svc = new RoslynCompilationService();
            var options = new CompilationOptions { AssemblyName = "Compile_SyntaxError" };

            var result = svc.Compile(BrokenSource, options);

            result.Success.Should().BeFalse();
            result.Assembly.Should().BeNull();
            result.Diagnostics.Should().Contain(d => d.Severity == CompilationDiagnosticSeverity.Error);
            // 줄/열은 1-based이고 유효 범위 내
            var first = result.Diagnostics.First(d => d.Severity == CompilationDiagnosticSeverity.Error);
            first.StartLine.Should().BeGreaterThan(0);
            first.StartColumn.Should().BeGreaterThan(0);
            first.Id.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Analyze_ReturnsDiagnostics_WithoutEmit()
        {
            var svc = new RoslynCompilationService();
            var options = new CompilationOptions { AssemblyName = "AnalyzeOnly" };

            var diags = svc.Analyze(BrokenSource, options);

            diags.Should().Contain(d => d.Severity == CompilationDiagnosticSeverity.Error);
        }

        [Fact]
        public void Analyze_ValidCode_ReturnsNoErrors()
        {
            var svc = new RoslynCompilationService();
            var options = new CompilationOptions { AssemblyName = "AnalyzeValid" };

            var diags = svc.Analyze(AddSource, options);

            diags.Should().NotContain(d => d.Severity == CompilationDiagnosticSeverity.Error);
        }

        [Fact]
        public void Compile_CanReferenceHostAssembly_UsingFluentAssertionsFromUserCode()
        {
            // 호스트 어셈블리 자동 수집 검증: 사용자 코드가 호스트에 이미 로드된 FluentAssertions를 사용 가능해야 한다.
            const string source = @"
using FluentAssertions;
namespace UserCode
{
    public static class Probe
    {
        public static bool IsFluentAvailable()
        {
            var asm = typeof(FluentAssertions.AssertionExtensions).Assembly;
            return asm != null;
        }
    }
}";
            var svc = new RoslynCompilationService();
            var options = new CompilationOptions { AssemblyName = "HostRefProbe" };

            var result = svc.Compile(source, options);

            var diagText = string.Join("; ", result.Diagnostics);
            result.Success.Should().BeTrue($"diagnostics: {diagText}");
            var ok = (bool)result.Assembly.GetType("UserCode.Probe").GetMethod("IsFluentAvailable").Invoke(null, null);
            ok.Should().BeTrue();
        }

        [Fact]
        public void UnloadAssembly_UnknownAssembly_ReturnsFalse()
        {
            var svc = new RoslynCompilationService();
            svc.UnloadAssembly(typeof(string).Assembly).Should().BeFalse();
            svc.UnloadAssembly(null).Should().BeFalse();
        }

        [Fact]
        public void Compile_TwoSeparateCompilations_ProduceIsolatedAssemblies()
        {
            var svc = new RoslynCompilationService();
            var optionsA = new CompilationOptions { AssemblyName = "IsoA" };
            var optionsB = new CompilationOptions { AssemblyName = "IsoB" };

            var a = svc.Compile(AddSource, optionsA);
            var b = svc.Compile(AddSource, optionsB);

            a.Success.Should().BeTrue();
            b.Success.Should().BeTrue();
            a.Assembly.Should().NotBeSameAs(b.Assembly);
            // 같은 source여도 별도 ALC에 로드되므로 타입 동일성도 다름.
            a.Assembly.GetType("UserCode.MyMath").Should().NotBeSameAs(b.Assembly.GetType("UserCode.MyMath"));

            svc.UnloadAssembly(a.Assembly).Should().BeTrue();
            svc.UnloadAssembly(b.Assembly).Should().BeTrue();
        }

        [Fact]
        public void Compile_ImplicitUsings_AreApplied()
        {
            const string source = @"
namespace UserCode
{
    public static class Greet
    {
        public static string Hello(string name) => StringBuilderHelper(name);
        private static string StringBuilderHelper(string s)
        {
            var sb = new StringBuilder();
            sb.Append(""Hello, "").Append(s);
            return sb.ToString();
        }
    }
}";
            var svc = new RoslynCompilationService();
            var options = new CompilationOptions { AssemblyName = "ImplicitUsings" };
            options.ImplicitUsings.Add("System.Text");

            var result = svc.Compile(source, options);

            var diagText = string.Join("; ", result.Diagnostics);
            result.Success.Should().BeTrue($"diagnostics: {diagText}");
            var name = (string)result.Assembly.GetType("UserCode.Greet").GetMethod("Hello").Invoke(null, new object[] { "World" });
            name.Should().Be("Hello, World");
        }
    }
}
