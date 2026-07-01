using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Nodes
{
    /// <summary>
    /// 묶음 B.5 — Constant-like 메소드를 그래프 노드로 래핑한 CustomConstantNode (TDD).
    /// </summary>
    [Xunit.Collection(nameof(NodeMetadataRegistryCollection))]
    public class CustomConstantNodeTests
    {
        public CustomConstantNodeTests()
        {
            BuiltInNodes.EnsureRegistered();
        }

        // 매개변수 없는 메소드 — Pi 상수.
        private static double GetPi() => 3.14159;

        [Fact]
        public async Task CustomConstantNode_FromParameterlessMethod_ProducesValue()
        {
            var method = typeof(CustomConstantNodeTests).GetMethod(
                nameof(GetPi), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var pins = SignatureToPinsBuilder.BuildAsConstant(method);
            var node = new CustomConstantNode("Test.Pi", method, pins);

            var meta = new NodeMetadata("Test.Pi", "Pi", "Math", "", 0,
                typeof(CustomConstantNode), () => new CustomConstantNode("Test.Pi", method, pins), pins);
            try
            {
                NodeMetadataRegistry.Register(meta);

                var g = new NodeGraph();
                var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
                var piNode = g.AddNode("Test.Pi", 0, 0);
                var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
                ((NodeBase)output).SetLiteralInput("Key", "pi");
                g.Connect(piNode.Id, "Out", output.Id, "Value");
                g.Connect(start.Id, "Then", output.Id, "In");

                var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

                result.Outputs["pi"].Should().Be(3.14159);
            }
            finally
            {
                NodeMetadataRegistry.UnregisterForTests("Test.Pi");
            }
        }

        [Fact]
        public void CustomConstantNode_PinSpec_OnlyDataOut()
        {
            var method = typeof(CustomConstantNodeTests).GetMethod(
                nameof(GetPi), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var pins = SignatureToPinsBuilder.BuildAsConstant(method);
            var node = new CustomConstantNode("Test.Pi2", method, pins);

            node.Pins.Should().HaveCount(1);
            node.Pins[0].Id.Should().Be("Out");
        }
    }
}
