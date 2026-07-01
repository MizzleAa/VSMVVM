using System.Collections.Generic;
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
    /// 묶음 C.1 — List 조작 노드 (TDD). Phase K — 다형성: 단일 typeId + 인스턴스 ItemType.
    /// </summary>
    public class ListNodeTests
    {
        public ListNodeTests()
        {
            BuiltInNodes.EnsureRegistered();
        }

        [Fact]
        public void ListNodes_TypeIds_AreNonGeneric()
        {
            ListAddNode.TypeIdConst.Should().Be("Core.List.Add");
            ListGetNode.TypeIdConst.Should().Be("Core.List.Get");
            ListCountNode.TypeIdConst.Should().Be("Core.List.Count");
        }

        private static T NewListNode<T>(NodeGraph g, System.Type itemType) where T : PolymorphicListNodeBase
        {
            var typeId = typeof(T).GetField("TypeIdConst").GetValue(null) as string;
            var node = (T)g.AddNode(typeId, 0, 0);
            node.ItemType = itemType;
            return node;
        }

        [Fact]
        public async Task ListAdd_AppendsItem_AndPropagatesList()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var add = NewListNode<ListAddNode>(g, typeof(int));
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);

            add.SetLiteralInput("List", new List<int> { 1, 2, 3 });
            add.SetLiteralInput("Item", 99);
            ((NodeBase)output).SetLiteralInput("Key", "list");

            g.Connect(start.Id, "Then", add.Id, "In");
            g.Connect(add.Id, "Then", output.Id, "In");
            g.Connect(add.Id, "Out", output.Id, "Value");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

            var list = (List<int>)result.Outputs["list"];
            list.Should().Equal(1, 2, 3, 99);
        }

        [Fact]
        public async Task ListGet_ReturnsElementAtIndex()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var get = NewListNode<ListGetNode>(g, typeof(string));
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            get.SetLiteralInput("List", new List<string> { "a", "b", "c" });
            get.SetLiteralInput("Index", 1);
            ((NodeBase)output).SetLiteralInput("Key", "v");

            g.Connect(get.Id, "Item", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["v"].Should().Be("b");
        }

        [Fact]
        public async Task ListCount_ReturnsLength()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var count = NewListNode<ListCountNode>(g, typeof(int));
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            count.SetLiteralInput("List", new List<int> { 10, 20, 30 });
            ((NodeBase)output).SetLiteralInput("Key", "n");

            g.Connect(count.Id, "Count", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["n"].Should().Be(3);
        }

        [Fact]
        public async Task ListCount_NullList_Returns0()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var count = NewListNode<ListCountNode>(g, typeof(int));
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)output).SetLiteralInput("Key", "n");

            g.Connect(count.Id, "Count", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["n"].Should().Be(0);
        }

        [Fact]
        public async Task ListClear_EmptiesTheList()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var clear = NewListNode<ListClearNode>(g, typeof(int));
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            clear.SetLiteralInput("List", new List<int> { 1, 2, 3 });
            ((NodeBase)output).SetLiteralInput("Key", "list");

            g.Connect(start.Id, "Then", clear.Id, "In");
            g.Connect(clear.Id, "Then", output.Id, "In");
            g.Connect(clear.Id, "Out", output.Id, "Value");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            ((List<int>)result.Outputs["list"]).Should().BeEmpty();
        }

        [Theory]
        [InlineData(2, true)]
        [InlineData(99, false)]
        public async Task ListContains_ReturnsBool(int needle, bool expected)
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var contains = NewListNode<ListContainsNode>(g, typeof(int));
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            contains.SetLiteralInput("List", new List<int> { 1, 2, 3 });
            contains.SetLiteralInput("Item", needle);
            ((NodeBase)output).SetLiteralInput("Key", "r");

            g.Connect(contains.Id, "Result", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            result.Outputs["r"].Should().Be(expected);
        }

        [Fact]
        public async Task ListRemoveAt_RemovesAtIndex()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var rm = NewListNode<ListRemoveAtNode>(g, typeof(int));
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            rm.SetLiteralInput("List", new List<int> { 1, 2, 3, 4 });
            rm.SetLiteralInput("Index", 1);
            ((NodeBase)output).SetLiteralInput("Key", "list");

            g.Connect(start.Id, "Then", rm.Id, "In");
            g.Connect(rm.Id, "Then", output.Id, "In");
            g.Connect(rm.Id, "Out", output.Id, "Value");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            ((List<int>)result.Outputs["list"]).Should().Equal(1, 3, 4);
        }

        [Fact]
        public async Task ListRemoveAt_IndexOutOfRange_NoOp()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var rm = NewListNode<ListRemoveAtNode>(g, typeof(int));
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            rm.SetLiteralInput("List", new List<int> { 1, 2 });
            rm.SetLiteralInput("Index", 99);
            ((NodeBase)output).SetLiteralInput("Key", "list");

            g.Connect(start.Id, "Then", rm.Id, "In");
            g.Connect(rm.Id, "Then", output.Id, "In");
            g.Connect(rm.Id, "Out", output.Id, "Value");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            ((List<int>)result.Outputs["list"]).Should().Equal(1, 2);
        }
    }
}
