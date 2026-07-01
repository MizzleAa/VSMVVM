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
    /// 묶음 B.5 — CustomNodeFactory 가 매개변수 0개 [MethodNode] 메소드를 CustomConstantNode 로 등록하는지.
    /// </summary>
    [Collection(nameof(NodeMetadataRegistryCollection))]
    public class CustomNodeFactoryConstantTests
    {
        public CustomNodeFactoryConstantTests()
        {
            BuiltInNodes.EnsureRegistered();
        }

        private static class Sample
        {
            [MethodNode("Test.GetPi", Category = "Math")]
            public static double GetPi() => 3.14159;
        }

        [Fact]
        public async Task RegisterFromAssembly_ParameterlessMethod_RegisteredAsCustomConstantNode()
        {
            try
            {
                // 같은 어셈블리에 [MethodNode] 픽스처를 가진 다른 테스트가 RegisterFromAssembly 를 호출한 뒤
                // 정리 안 했으면 본 테스트의 RegisterFromAssembly 가 모든 등록을 silently skip 하여 결과가 비게 된다.
                // 본 테스트가 검증하는 typeId 만 명시적으로 unregister 후 호출 — 다른 typeId 들은 이미 등록 상태일 수 있으나
                // 본 테스트는 자기 typeId 가 결과에 들어가는지만 본다.
                NodeMetadataRegistry.UnregisterForTests("Test.GetPi");
                var registered = CustomNodeFactory.RegisterFromAssembly(typeof(CustomNodeFactoryConstantTests).Assembly);
                registered.Should().Contain("Test.GetPi");

                var meta = NodeMetadataRegistry.Get("Test.GetPi");
                meta.Should().NotBeNull();
                // ClrType 이 CustomConstantNode 여야 함 (Function 이 아닌).
                meta.ClrType.Should().Be(typeof(CustomConstantNode));

                // 핀: Out 1개만.
                meta.Pins.Should().ContainSingle(p => p.Id == "Out");

                // 그래프에 추가 후 실행 — 결과 검증.
                var g = new NodeGraph();
                var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
                var pi = g.AddNode("Test.GetPi", 0, 0);
                var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
                ((NodeBase)output).SetLiteralInput("Key", "pi");
                g.Connect(pi.Id, "Out", output.Id, "Value");
                g.Connect(start.Id, "Then", output.Id, "In");

                var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
                result.Outputs["pi"].Should().Be(3.14159);
            }
            finally
            {
                NodeMetadataRegistry.UnregisterForTests("Test.GetPi");
            }
        }
    }
}
