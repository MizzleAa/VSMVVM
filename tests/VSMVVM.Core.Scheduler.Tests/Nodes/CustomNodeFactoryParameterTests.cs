using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Attributes;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Nodes
{
    /// <summary>
    /// Phase L1 — [ParameterNode] 정적 필드 → CustomParameterNode 등록 + 실행.
    /// </summary>
    [Collection(nameof(NodeMetadataRegistryCollection))]
    public class CustomNodeFactoryParameterTests
    {
        public CustomNodeFactoryParameterTests()
        {
            BuiltInNodes.EnsureRegistered();
        }

        // 인스턴스 필드 케이스도 표현해야 하므로 non-static 클래스. 인스턴스 자체는 생성하지 않음 (필드는 정적 케이스만 사용).
        private class Sample
        {
            [ParameterNode("Test.Param.Score", Category = "Parameters")]
            public static int Score = 42;

            [ParameterNode("Test.Param.Greeting")]
            public static string Greeting = "hello";

            [ParameterNode("Test.Param.Nums")]
            public static List<double> Nums = new() { 1.0, 2.0, 3.0 };

            [ParameterNode("Test.Param.MaybeNull")]
            public static string MaybeNull = null;

            // attribute 미부착 — 무시되어야 함.
            public static int IgnoredField = 99;

            // 인스턴스 필드 — silent skip 되어야 함.
#pragma warning disable CS0649
            [ParameterNode("Test.Param.InstanceField")]
            public int InstanceField;
#pragma warning restore CS0649
        }

        [Fact]
        public async Task RegisterFromAssembly_StaticField_RegisteredAsCustomParameterNode()
        {
            NodeMetadataRegistry.UnregisterForTests("Test.Param.Score");
            var registered = CustomNodeFactory.RegisterFromAssembly(typeof(CustomNodeFactoryParameterTests).Assembly);
            registered.Should().Contain("Test.Param.Score");

            var meta = NodeMetadataRegistry.Get("Test.Param.Score");
            meta.Should().NotBeNull();
            meta.ClrType.Should().Be(typeof(CustomParameterNode));
            meta.Category.Should().Be("Parameters");
            meta.Pins.Should().ContainSingle(p => p.Id == "Out");
            meta.Pins[0].ValueType.Should().Be(typeof(int));

            // End-to-end Run — 필드값 42 가 Out 으로 흐른다.
            Sample.Score = 42;
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var param = g.AddNode("Test.Param.Score", 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)output).SetLiteralInput("Key", "v");
            g.Connect(param.Id, "Out", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["v"].Should().Be(42);
        }

        [Fact]
        public async Task ReferenceTypeField_NullValueIsAllowed()
        {
            NodeMetadataRegistry.UnregisterForTests("Test.Param.MaybeNull");
            CustomNodeFactory.RegisterFromAssembly(typeof(CustomNodeFactoryParameterTests).Assembly);

            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var param = g.AddNode("Test.Param.MaybeNull", 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)output).SetLiteralInput("Key", "v");
            g.Connect(param.Id, "Out", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["v"].Should().BeNull();
        }

        [Fact]
        public void NonAttributedField_IsIgnored()
        {
            NodeMetadataRegistry.UnregisterForTests("Test.Param.IgnoredField");
            CustomNodeFactory.RegisterFromAssembly(typeof(CustomNodeFactoryParameterTests).Assembly);
            NodeMetadataRegistry.Get("Test.Param.IgnoredField").Should().BeNull();
        }

        [Fact]
        public void InstanceField_IsSilentlySkipped()
        {
            NodeMetadataRegistry.UnregisterForTests("Test.Param.InstanceField");
            CustomNodeFactory.RegisterFromAssembly(typeof(CustomNodeFactoryParameterTests).Assembly);
            NodeMetadataRegistry.Get("Test.Param.InstanceField").Should().BeNull();
        }

        [Fact]
        public async Task FieldValue_IsReadAtEachEvaluate_NotCached()
        {
            NodeMetadataRegistry.UnregisterForTests("Test.Param.Greeting");
            CustomNodeFactory.RegisterFromAssembly(typeof(CustomNodeFactoryParameterTests).Assembly);

            // 첫 실행 — 초기값 "hello".
            Sample.Greeting = "hello";
            var v1 = await RunAndReadGreeting();
            v1.Should().Be("hello");

            // 필드 변경 후 재실행 — 새 값이 흘러야 한다.
            Sample.Greeting = "world";
            var v2 = await RunAndReadGreeting();
            v2.Should().Be("world");
        }

        private static async Task<object> RunAndReadGreeting()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var param = g.AddNode("Test.Param.Greeting", 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)output).SetLiteralInput("Key", "v");
            g.Connect(param.Id, "Out", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");
            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            return result.Outputs["v"];
        }

        [Fact]
        public void DuplicateRegistration_IsSwallowed()
        {
            NodeMetadataRegistry.UnregisterForTests("Test.Param.Nums");
            CustomNodeFactory.RegisterFromAssembly(typeof(CustomNodeFactoryParameterTests).Assembly);
            // 두 번째 호출 — 이미 등록되어 있어도 throw 안 함.
            var act = () => CustomNodeFactory.RegisterFromAssembly(typeof(CustomNodeFactoryParameterTests).Assembly);
            act.Should().NotThrow();
        }
    }
}
