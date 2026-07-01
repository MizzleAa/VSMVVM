using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Serialization;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Serialization
{
    /// <summary>
    /// 묶음 B.3 — GraphVariable 정의 + Variable 노드의 VariableName/ItemType 이 JSON 라운드트립에 보존되는지.
    /// </summary>
    public class GraphVariableSerializationTests
    {
        public GraphVariableSerializationTests()
        {
            BuiltInNodes.EnsureRegistered();
        }

        [Fact]
        public void Variables_RoundTrip_Preserved()
        {
            var g = new NodeGraph();
            g.AddVariable("count", typeof(int), 42);
            g.AddVariable("name", typeof(string), "hello");

            var json = NodeGraphSerializer.Serialize(g);
            var loaded = NodeGraphSerializer.Deserialize(json);

            loaded.Variables.Should().ContainKey("count");
            loaded.Variables["count"].ClrType.Should().Be(typeof(int));
            loaded.Variables["count"].DefaultValue.Should().Be(42);
            loaded.Variables["name"].ClrType.Should().Be(typeof(string));
            loaded.Variables["name"].DefaultValue.Should().Be("hello");
        }

        [Fact]
        public void GetVariableNode_VariableNameAndItemType_RoundTrip()
        {
            var g = new NodeGraph();
            g.AddVariable("x", typeof(int), 0);
            var get = (GetVariableNode)g.AddNode(GetVariableNode.TypeIdConst, 0, 0);
            get.ItemType = typeof(int);
            get.VariableName = "x";

            var json = NodeGraphSerializer.Serialize(g);
            var loaded = NodeGraphSerializer.Deserialize(json);

            var loadedGet = (GetVariableNode)loaded.Nodes.Should().ContainSingle().Subject;
            loadedGet.VariableName.Should().Be("x");
            loadedGet.ItemType.Should().Be(typeof(int));
        }

        [Fact]
        public void SetVariableNode_VariableNameAndItemType_RoundTrip()
        {
            var g = new NodeGraph();
            g.AddVariable("flag", typeof(bool), false);
            var set = (SetVariableNode)g.AddNode(SetVariableNode.TypeIdConst, 0, 0);
            set.ItemType = typeof(bool);
            set.VariableName = "flag";

            var json = NodeGraphSerializer.Serialize(g);
            var loaded = NodeGraphSerializer.Deserialize(json);

            var loadedSet = (SetVariableNode)loaded.Nodes.Should().ContainSingle().Subject;
            loadedSet.VariableName.Should().Be("flag");
            loadedSet.ItemType.Should().Be(typeof(bool));
        }
    }
}
