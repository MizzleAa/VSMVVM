using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Compilation;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.Core.Scheduler.Scripting;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Scripting.Tests
{
    /// <summary>
    /// Phase 3b + 3c end-to-end: 사용자 C# 코드를 Roslyn으로 동적 컴파일하고,
    /// CustomNodeFactory로 [MethodNode]가 부착된 메소드를 NodeMetadataRegistry에 등록하고,
    /// SchedulerService로 그래프 실행까지 통합 검증.
    /// </summary>
    public class RoslynCustomNodeIntegrationTests
    {
        private const string AddSource = @"
using VSMVVM.Core.Scheduler.Attributes;
namespace UserCode
{
    public static class UserMath
    {
        [MethodNode(""User.Add"", Category = ""Math"")]
        public static int Add(int a, int b) => a + b;
    }
}";

        [Fact]
        public async Task EndToEnd_UserAdd_CompilesAndRunsInGraph_ProducesSum()
        {
            const string typeId = "User.Add";
            NodeMetadataRegistry.UnregisterForTests(typeId);

            // 1) Roslyn으로 컴파일
            var compiler = new RoslynCompilationService();
            var options = new CompilationOptions { AssemblyName = "EndToEnd_UserAdd" };
            var compResult = compiler.Compile(AddSource, options);

            var diagText = string.Join("; ", compResult.Diagnostics);
            compResult.Success.Should().BeTrue($"diagnostics: {diagText}");

            try
            {
                // 2) CustomNodeFactory로 컴파일된 어셈블리에서 [MethodNode] 메소드를 NodeMetadataRegistry에 등록
                var registered = CustomNodeFactory.RegisterFromAssembly(compResult.Assembly);
                registered.Should().Contain(typeId);

                // 3) 그래프 작성: Start --> User.Add --> End, a/b는 ValueProducer로 공급
                BuiltInNodes.EnsureRegistered();
                var graph = new NodeGraph();
                var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
                var addNode = graph.AddNode(typeId, 0, 0);
                var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);
                var aProd = new IntProducer(40);
                var bProd = new IntProducer(2);
                graph.AddNode(aProd, 0, 0);
                graph.AddNode(bProd, 0, 0);

                graph.Connect(start.Id, "Then", addNode.Id, "In");
                graph.Connect(aProd.Id, "Value", addNode.Id, "a");
                graph.Connect(bProd.Id, "Value", addNode.Id, "b");
                graph.Connect(addNode.Id, "Then", end.Id, "In");

                // 4) 실행 + Result 검증
                var ctx = new ExecutionContext(graph);
                var runResult = await new SchedulerService().RunAsync(graph, start.Id, ctx);

                runResult.Status.Should().Be(ExecutionStatus.Completed);
                ctx.DataCacheSnapshot[(addNode.Id, "Result")].Should().Be(42);
            }
            finally
            {
                NodeMetadataRegistry.UnregisterForTests(typeId);
                compiler.UnloadAssembly(compResult.Assembly);
            }
        }

        // 위 테스트에 필요한 최소 ValueProducer (Scripting.Tests는 Core.Scheduler.Tests를 참조하지 않으므로 별도 정의)
        private sealed class IntProducer : NodeBase
        {
            private readonly int _value;
            public IntProducer(int value) { _value = value; }
            public override string TypeId => "Test.IntProducer";
            protected override System.Collections.Generic.IReadOnlyList<PinDescriptor> GetPinDescriptors() => new[]
            {
                new PinDescriptor("Value", "Value", PinDirection.Output, PinKind.Data, typeof(int), 0),
            };
            public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
            {
                context.SetOutput(this, "Value", _value);
                return Task.FromResult(ExecutionFlow.Halt);
            }
        }
    }
}
