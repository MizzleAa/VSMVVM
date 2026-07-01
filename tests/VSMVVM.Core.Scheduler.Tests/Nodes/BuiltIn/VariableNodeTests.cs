using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Nodes.BuiltIn
{
    /// <summary>
    /// 묶음 B.2 — Variable Get/Set 노드 (TDD). Phase K — 다형성: 단일 typeId + 인스턴스 ItemType/VariableName.
    /// </summary>
    public class VariableNodeTests
    {
        public VariableNodeTests()
        {
            BuiltInNodes.EnsureRegistered();
        }

        [Fact]
        public async Task GetVariableNode_ReturnsDefaultValue_WhenNotSetInContext()
        {
            var g = new NodeGraph();
            g.AddVariable("x", typeof(int), 42);

            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var get = (GetVariableNode)g.AddNode(GetVariableNode.TypeIdConst, 0, 0);
            get.ItemType = typeof(int);
            get.VariableName = "x";
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)output).SetLiteralInput("Key", "v");
            g.Connect(get.Id, "Value", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

            result.Outputs["v"].Should().Be(42, "Variable.DefaultValue 가 ExecutionContext.Variables 초기화에 사용됨");
        }

        [Fact]
        public async Task SetVariableNode_UpdatesContext_AndSubsequentGetReturnsNewValue()
        {
            var g = new NodeGraph();
            g.AddVariable("x", typeof(int), 0);

            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var set = (SetVariableNode)g.AddNode(SetVariableNode.TypeIdConst, 0, 0);
            set.ItemType = typeof(int);
            set.VariableName = "x";
            ((NodeBase)set).SetLiteralInput("Value", 100);
            var get = (GetVariableNode)g.AddNode(GetVariableNode.TypeIdConst, 0, 0);
            get.ItemType = typeof(int);
            get.VariableName = "x";
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)output).SetLiteralInput("Key", "v");

            g.Connect(start.Id, "Then", set.Id, "In");
            g.Connect(set.Id, "Then", output.Id, "In");
            g.Connect(get.Id, "Value", output.Id, "Value");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

            result.Outputs["v"].Should().Be(100);
        }

        [Fact]
        public void VariableNodes_TypeIds_AreNonGeneric()
        {
            GetVariableNode.TypeIdConst.Should().Be("Core.Variable.Get");
            SetVariableNode.TypeIdConst.Should().Be("Core.Variable.Set");
        }

        [Fact]
        public void Metadata_HasTypeParameterT()
        {
            var meta = NodeMetadataRegistry.Get(GetVariableNode.TypeIdConst);
            meta.Should().NotBeNull();
            meta.IsPolymorphic.Should().BeTrue();
            meta.TypeParameters.Should().Equal("T");
        }
    }
}
