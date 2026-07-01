using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Nodes.BuiltIn
{
    /// <summary>
    /// Phase L — 다형성 ConstantNode. 단일 typeId "Core.Constant" + 인스턴스의 ItemType 속성.
    /// </summary>
    public class ConstantNodePolymorphismTests
    {
        public ConstantNodePolymorphismTests()
        {
            BuiltInNodes.EnsureRegistered();
        }

        [Fact]
        public void TypeId_IsNonGeneric_AndStable()
        {
            ConstantNode.TypeIdConst.Should().Be("Core.Constant");
            var meta = NodeMetadataRegistry.Get(ConstantNode.TypeIdConst);
            meta.Should().NotBeNull();
            meta.IsPolymorphic.Should().BeTrue();
            meta.TypeParameters.Should().Equal("T");
            meta.Category.Should().Be("Math");
        }

        [Fact]
        public void DefaultItemType_IsInt_PinsTyped()
        {
            var node = new ConstantNode();
            node.ItemType.Should().Be(typeof(int));
            FindPin(node, "Value").ValueType.Should().Be(typeof(int));
            FindPin(node, "Out").ValueType.Should().Be(typeof(int));
        }

        [Fact]
        public void ChangeItemType_RebuildsPins()
        {
            var node = new ConstantNode { ItemType = typeof(double) };
            FindPin(node, "Value").ValueType.Should().Be(typeof(double));
            FindPin(node, "Out").ValueType.Should().Be(typeof(double));

            node.ItemType = typeof(string);
            FindPin(node, "Value").ValueType.Should().Be(typeof(string));
            FindPin(node, "Out").ValueType.Should().Be(typeof(string));
        }

        [Theory]
        [InlineData(typeof(int), 123)]
        [InlineData(typeof(double), 3.14)]
        [InlineData(typeof(string), "hello")]
        [InlineData(typeof(bool), true)]
        public async Task RunsWithLiteralValue_PerItemType(Type itemType, object literal)
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var c = (ConstantNode)g.AddNode(ConstantNode.TypeIdConst, 0, 0);
            c.ItemType = itemType;
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            c.SetLiteralInput("Value", literal);
            ((NodeBase)output).SetLiteralInput("Key", "v");
            g.Connect(c.Id, "Out", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["v"].Should().Be(literal);
        }

        [Fact]
        public async Task LongType_NoPreRegistrationNeeded()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var c = (ConstantNode)g.AddNode(ConstantNode.TypeIdConst, 0, 0);
            c.ItemType = typeof(long);
            c.SetLiteralInput("Value", 9_000_000_000L);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)output).SetLiteralInput("Key", "v");
            g.Connect(c.Id, "Out", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["v"].Should().Be(9_000_000_000L);
        }

        [Fact]
        public void WriteState_PreservesItemType_AcrossReadState()
        {
            var c = new ConstantNode { ItemType = typeof(double) };
            using var ms = new System.IO.MemoryStream();
            using (var writer = new System.Text.Json.Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                c.WriteState(writer);
                writer.WriteEndObject();
            }
            var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            var loaded = new ConstantNode();
            loaded.ReadState(doc.RootElement);
            loaded.ItemType.Should().Be(typeof(double));
        }

        private static IPin FindPin(INode node, string id)
        {
            foreach (var p in node.Pins) if (p.Id == id) return p;
            throw new InvalidOperationException(id);
        }
    }
}
