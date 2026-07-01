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
    /// Phase K — 단일 typeId 다형성 ListAddNode. 인스턴스의 ItemType 으로 핀 타입 결정.
    /// </summary>
    public class PolymorphicListAddNodeTests
    {
        [Fact]
        public void TypeId_IsNotGeneric_AndStable()
        {
            ListAddNode.TypeIdConst.Should().Be("Core.List.Add");
        }

        [Fact]
        public void DefaultItemType_IsObject_ListPinIsListOfObject()
        {
            var node = new ListAddNode();
            node.ItemType.Should().Be(typeof(object));
            FindPin(node, "Item").ValueType.Should().Be(typeof(object));
            FindPin(node, "List").ValueType.Should().Be(typeof(List<object>));
            FindPin(node, "Out").ValueType.Should().Be(typeof(List<object>));
        }

        [Fact]
        public void ChangeItemType_RebuildsPinsToConcreteType()
        {
            var node = new ListAddNode { ItemType = typeof(int) };
            FindPin(node, "Item").ValueType.Should().Be(typeof(int));
            FindPin(node, "List").ValueType.Should().Be(typeof(List<int>));
            FindPin(node, "Out").ValueType.Should().Be(typeof(List<int>));
        }

        [Fact]
        public async Task Execute_AppendsItem_ToListOfMatchingType()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var add = (ListAddNode)g.AddNode(ListAddNode.TypeIdConst, 0, 0);
            add.ItemType = typeof(int);
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            var list = new List<int> { 1, 2 };
            ((NodeBase)add).SetLiteralInput("List", list);
            ((NodeBase)add).SetLiteralInput("Item", 3);
            g.Connect(start.Id, "Then", add.Id, "In");
            g.Connect(add.Id, "Then", end.Id, "In");

            var ctx = new ExecutionContext(g);
            var result = await new SchedulerService().RunAsync(g, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            list.Should().Equal(1, 2, 3);
        }

        [Fact]
        public void Metadata_HasTypeParameterT()
        {
            BuiltInNodes.EnsureRegistered();
            var meta = NodeMetadataRegistry.Get(ListAddNode.TypeIdConst);
            meta.Should().NotBeNull();
            meta.IsPolymorphic.Should().BeTrue();
            meta.TypeParameters.Should().Equal("T");
            meta.Category.Should().Be("Collections");
        }

        private static IPin FindPin(INode node, string id)
        {
            foreach (var p in node.Pins) if (p.Id == id) return p;
            throw new InvalidOperationException(id);
        }
    }
}
