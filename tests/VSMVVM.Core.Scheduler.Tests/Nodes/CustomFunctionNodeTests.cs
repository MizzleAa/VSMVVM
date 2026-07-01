using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Attributes;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.Core.Scheduler.Tests.TestHelpers;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Nodes
{
    // 테스트에서 사용할 메소드들
    public static class MathOps
    {
        [MethodNode("Test.MathOps.Add", Category = "Math")]
        public static int Add(int a, int b) => a + b;

        [MethodNode("Test.MathOps.MultiplyAsync", Category = "Math")]
        public static async Task<int> MultiplyAsync(int a, int b)
        {
            await Task.Yield();
            return a * b;
        }

        [MethodNode("Test.MathOps.NoteValue", Category = "Math")]
        public static void NoteValue(int v)
        {
            LastNoted = v;
        }

        public static int LastNoted;
    }

    [Collection(nameof(NodeMetadataRegistryCollection))]
    public class CustomFunctionNodeTests
    {
        [Fact]
        public async Task CustomFunctionNode_DirectInstance_AddInts_ProducesSumOnResultPin()
        {
            var method = typeof(MathOps).GetMethod(nameof(MathOps.Add));
            var pins = SignatureToPinsBuilder.Build(method);
            var node = new CustomFunctionNode("Direct.Add", method, pins);

            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            graph.AddNode(node, 0, 0);
            var aProd = new ValueProducerNode<int>(7);
            var bProd = new ValueProducerNode<int>(35);
            graph.AddNode(aProd, 0, 0);
            graph.AddNode(bProd, 0, 0);

            graph.Connect(start.Id, "Then", node.Id, "In");
            graph.Connect(aProd.Id, "Value", node.Id, "a");
            graph.Connect(bProd.Id, "Value", node.Id, "b");

            var ctx = new ExecutionContext(graph);
            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            // Result 핀에 42가 들어있어야 함
            ctx.DataCacheSnapshot[(node.Id, "Result")].Should().Be(42);
        }

        [Fact]
        public async Task CustomFunctionNode_AsyncMethod_AwaitsAndReturnsResult()
        {
            var method = typeof(MathOps).GetMethod(nameof(MathOps.MultiplyAsync));
            var pins = SignatureToPinsBuilder.Build(method);
            var node = new CustomFunctionNode("Direct.MultiplyAsync", method, pins);

            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            graph.AddNode(node, 0, 0);
            var aProd = new ValueProducerNode<int>(6);
            var bProd = new ValueProducerNode<int>(7);
            graph.AddNode(aProd, 0, 0);
            graph.AddNode(bProd, 0, 0);

            graph.Connect(start.Id, "Then", node.Id, "In");
            graph.Connect(aProd.Id, "Value", node.Id, "a");
            graph.Connect(bProd.Id, "Value", node.Id, "b");

            var ctx = new ExecutionContext(graph);
            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            ctx.DataCacheSnapshot[(node.Id, "Result")].Should().Be(42);
        }

        [Fact]
        public async Task CustomFunctionNode_VoidMethod_ExecutesAndContinuesThen()
        {
            MathOps.LastNoted = 0;

            var method = typeof(MathOps).GetMethod(nameof(MathOps.NoteValue));
            var pins = SignatureToPinsBuilder.Build(method);
            var node = new CustomFunctionNode("Direct.NoteValue", method, pins);

            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            graph.AddNode(node, 0, 0);
            var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            var vProd = new ValueProducerNode<int>(99);
            graph.AddNode(vProd, 0, 0);

            graph.Connect(start.Id, "Then", node.Id, "In");
            graph.Connect(vProd.Id, "Value", node.Id, "v");
            graph.Connect(node.Id, "Then", end.Id, "In");

            var ctx = new ExecutionContext(graph);
            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            MathOps.LastNoted.Should().Be(99);
            // Result 핀이 없어야 함
            ctx.DataCacheSnapshot.Should().NotContainKey((node.Id, "Result"));
        }

        [Fact]
        public void CustomNodeFactory_RegisterFromAssembly_RegistersMethodNodeAttributedMethods()
        {
            // 미리 정리 (다른 테스트가 등록했을 가능성 대비)
            NodeMetadataRegistry.UnregisterForTests("Test.MathOps.Add");
            NodeMetadataRegistry.UnregisterForTests("Test.MathOps.MultiplyAsync");
            NodeMetadataRegistry.UnregisterForTests("Test.MathOps.NoteValue");

            var registered = CustomNodeFactory.RegisterFromAssembly(typeof(MathOps).Assembly);

            registered.Should().Contain("Test.MathOps.Add");
            registered.Should().Contain("Test.MathOps.MultiplyAsync");
            registered.Should().Contain("Test.MathOps.NoteValue");

            var addMeta = NodeMetadataRegistry.Get("Test.MathOps.Add");
            addMeta.Should().NotBeNull();
            addMeta.DisplayName.Should().Be("Add");
            addMeta.Category.Should().Be("Math");
            addMeta.Pins.Should().HaveCount(5); // In, a, b, Then, Result

            // factory가 정상 동작
            var node = addMeta.Factory();
            node.Should().BeOfType<CustomFunctionNode>();
            node.TypeId.Should().Be("Test.MathOps.Add");

            // 정리
            NodeMetadataRegistry.UnregisterForTests("Test.MathOps.Add");
            NodeMetadataRegistry.UnregisterForTests("Test.MathOps.MultiplyAsync");
            NodeMetadataRegistry.UnregisterForTests("Test.MathOps.NoteValue");
        }

        [Fact]
        public async Task CustomNodeFactory_RegisteredNode_RunsViaGraphAddNodeByTypeId()
        {
            NodeMetadataRegistry.UnregisterForTests("Test.MathOps.Add");
            CustomNodeFactory.RegisterFromAssembly(typeof(MathOps).Assembly);
            try
            {
                BuiltInNodes.EnsureRegistered();
                var graph = new NodeGraph();
                var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
                var addNode = graph.AddNode("Test.MathOps.Add", 0, 0);   // ← TypeId로 생성
                var aProd = new ValueProducerNode<int>(20);
                var bProd = new ValueProducerNode<int>(22);
                graph.AddNode(aProd, 0, 0);
                graph.AddNode(bProd, 0, 0);

                graph.Connect(start.Id, "Then", addNode.Id, "In");
                graph.Connect(aProd.Id, "Value", addNode.Id, "a");
                graph.Connect(bProd.Id, "Value", addNode.Id, "b");

                var ctx = new ExecutionContext(graph);
                var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

                result.Status.Should().Be(ExecutionStatus.Completed);
                ctx.DataCacheSnapshot[(addNode.Id, "Result")].Should().Be(42);
            }
            finally
            {
                NodeMetadataRegistry.UnregisterForTests("Test.MathOps.Add");
                NodeMetadataRegistry.UnregisterForTests("Test.MathOps.MultiplyAsync");
                NodeMetadataRegistry.UnregisterForTests("Test.MathOps.NoteValue");
            }
        }

        [Fact]
        public void CustomNodeFactory_PublicStaticOptIn_RegistersNonAttributedMethods()
        {
            // PublicStaticHelper.Square 는 [MethodNode] 미부착이지만 RegisterPublicStaticMethods=true면 자동 등록.
            // 자동 등록의 typeId는 prefix.{ClassName}.{MethodName}
            var registered = CustomNodeFactory.RegisterFromAssembly(typeof(PublicStaticHelper).Assembly,
                new CustomNodeFactory.Options { RegisterPublicStaticMethods = true, AutoTypeIdPrefix = "Auto" });

            registered.Should().Contain(id => id == "Auto.PublicStaticHelper.Square");

            // 정리: 이 어셈블리의 자동 등록 대상은 많을 수 있으므로 우리가 추가한 항목만 제거
            foreach (var id in registered)
            {
                NodeMetadataRegistry.UnregisterForTests(id);
            }
        }
    }

    public static class PublicStaticHelper
    {
        public static int Square(int x) => x * x;
    }
}
