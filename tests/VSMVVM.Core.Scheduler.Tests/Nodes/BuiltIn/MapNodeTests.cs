using System.Collections.Generic;
using System.Linq;
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
    /// 묶음 C.2 — Dictionary&lt;K,V&gt; 조작 노드 (TDD).
    /// Set/Get/ContainsKey/Remove/Keys/Values/Count.
    /// </summary>
    public class MapNodeTests
    {
        public MapNodeTests()
        {
            BuiltInNodes.EnsureRegistered();
            BuiltInNodes.EnsureMapNodesRegistered<string, int>();
        }

        [Fact]
        public void MapNodes_GenericTypeId_EmbedsBothKVTypes()
        {
            MapSetNode<string, int>.TypeIdConst.Should().Be("Core.Map.Set<System.String,System.Int32>");
            MapGetNode<string, int>.TypeIdConst.Should().Be("Core.Map.Get<System.String,System.Int32>");
        }

        [Fact]
        public async Task MapSet_AddsOrUpdatesKey()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var set = (MapSetNode<string, int>)g.AddNode(MapSetNode<string, int>.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)set).SetLiteralInput("Map", new Dictionary<string, int>());
            ((NodeBase)set).SetLiteralInput("Key", "x");
            ((NodeBase)set).SetLiteralInput("Value", 42);
            ((NodeBase)output).SetLiteralInput("Key", "map");

            g.Connect(start.Id, "Then", set.Id, "In");
            g.Connect(set.Id, "Then", output.Id, "In");
            g.Connect(set.Id, "Out", output.Id, "Value");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            var dict = (Dictionary<string, int>)result.Outputs["map"];
            dict["x"].Should().Be(42);
        }

        [Fact]
        public async Task MapGet_ExistingKey_ReturnsValue()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var get = (MapGetNode<string, int>)g.AddNode(MapGetNode<string, int>.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)get).SetLiteralInput("Map", new Dictionary<string, int> { { "a", 1 }, { "b", 2 } });
            ((NodeBase)get).SetLiteralInput("Key", "b");
            ((NodeBase)output).SetLiteralInput("Key", "v");

            g.Connect(get.Id, "Value", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["v"].Should().Be(2);
        }

        [Fact]
        public async Task MapGet_MissingKey_ReturnsDefault()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var get = (MapGetNode<string, int>)g.AddNode(MapGetNode<string, int>.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)get).SetLiteralInput("Map", new Dictionary<string, int> { { "a", 1 } });
            ((NodeBase)get).SetLiteralInput("Key", "missing");
            ((NodeBase)output).SetLiteralInput("Key", "v");

            g.Connect(get.Id, "Value", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["v"].Should().Be(0);
        }

        [Theory]
        [InlineData("a", true)]
        [InlineData("z", false)]
        public async Task MapContainsKey(string key, bool expected)
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var cks = (MapContainsKeyNode<string, int>)g.AddNode(MapContainsKeyNode<string, int>.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)cks).SetLiteralInput("Map", new Dictionary<string, int> { { "a", 1 } });
            ((NodeBase)cks).SetLiteralInput("Key", key);
            ((NodeBase)output).SetLiteralInput("Key", "r");

            g.Connect(cks.Id, "Result", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["r"].Should().Be(expected);
        }

        [Fact]
        public async Task MapRemove_DeletesEntry()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var rm = (MapRemoveNode<string, int>)g.AddNode(MapRemoveNode<string, int>.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)rm).SetLiteralInput("Map", new Dictionary<string, int> { { "a", 1 }, { "b", 2 } });
            ((NodeBase)rm).SetLiteralInput("Key", "a");
            ((NodeBase)output).SetLiteralInput("Key", "map");

            g.Connect(start.Id, "Then", rm.Id, "In");
            g.Connect(rm.Id, "Then", output.Id, "In");
            g.Connect(rm.Id, "Out", output.Id, "Value");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            var dict = (Dictionary<string, int>)result.Outputs["map"];
            dict.Should().NotContainKey("a");
            dict.Should().ContainKey("b");
        }

        [Fact]
        public async Task MapKeys_ReturnsListOfKeys()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var keys = (MapKeysNode<string, int>)g.AddNode(MapKeysNode<string, int>.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)keys).SetLiteralInput("Map", new Dictionary<string, int> { { "a", 1 }, { "b", 2 } });
            ((NodeBase)output).SetLiteralInput("Key", "keys");

            g.Connect(keys.Id, "Keys", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            var list = (List<string>)result.Outputs["keys"];
            list.OrderBy(x => x).Should().Equal("a", "b");
        }

        [Fact]
        public async Task MapValues_ReturnsListOfValues()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var values = (MapValuesNode<string, int>)g.AddNode(MapValuesNode<string, int>.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)values).SetLiteralInput("Map", new Dictionary<string, int> { { "a", 1 }, { "b", 2 } });
            ((NodeBase)output).SetLiteralInput("Key", "vals");

            g.Connect(values.Id, "Values", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            var list = (List<int>)result.Outputs["vals"];
            list.OrderBy(x => x).Should().Equal(1, 2);
        }

        [Fact]
        public async Task MapCount_ReturnsSize()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var cnt = (MapCountNode<string, int>)g.AddNode(MapCountNode<string, int>.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)cnt).SetLiteralInput("Map", new Dictionary<string, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } });
            ((NodeBase)output).SetLiteralInput("Key", "n");

            g.Connect(cnt.Id, "Count", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["n"].Should().Be(3);
        }
    }
}
