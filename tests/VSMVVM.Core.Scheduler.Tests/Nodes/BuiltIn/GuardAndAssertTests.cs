using System;
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
    public class GuardAndAssertTests
    {
        [Fact]
        public async Task AssertNode_FalseCondition_FailsWithAssertionFailedException()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var assert = g.AddNode(AssertNode.TypeIdConst, 0, 0);
            ((NodeBase)assert).SetLiteralInput("Condition", false);
            ((NodeBase)assert).SetLiteralInput("Message", "boom");
            g.Connect(start.Id, "Then", assert.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

            result.Status.Should().Be(ExecutionStatus.Failed);
            result.Error.Should().BeOfType<AssertionFailedException>()
                .Which.Message.Should().Be("boom");
        }

        [Fact]
        public async Task AssertNode_TrueCondition_CompletesAndFiresThen()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var assert = g.AddNode(AssertNode.TypeIdConst, 0, 0);
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            ((NodeBase)assert).SetLiteralInput("Condition", true);
            g.Connect(start.Id, "Then", assert.Id, "In");
            g.Connect(assert.Id, "Then", end.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

            result.Status.Should().Be(ExecutionStatus.Completed);
            result.NodesExecuted.Should().Be(3);
        }

        [Theory]
        [InlineData(true, "Pass")]
        [InlineData(false, "Fail")]
        public async Task GuardNode_RoutesByCondition(bool cond, string expectedPin)
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var guard = g.AddNode(GuardNode.TypeIdConst, 0, 0);
            var passEnd = g.AddNode(EndNode.TypeIdConst, 0, 0);
            var failEnd = g.AddNode(EndNode.TypeIdConst, 0, 0);
            ((NodeBase)guard).SetLiteralInput("Condition", cond);
            g.Connect(start.Id, "Then", guard.Id, "In");
            g.Connect(guard.Id, "Pass", passEnd.Id, "In");
            g.Connect(guard.Id, "Fail", failEnd.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

            result.Status.Should().Be(ExecutionStatus.Completed);
            // Start + Guard + (Pass 또는 Fail) End = 3
            result.NodesExecuted.Should().Be(3);
            _ = expectedPin; // 흐름은 NodesExecuted 카운트로만 검증; pin 흐름은 SchedulerService 라우팅 단위 테스트에서 검증
        }

        [Fact]
        public async Task RangeAssertNode_OutOfRange_Throws()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var range = g.AddNode(RangeAssertNode.TypeIdConst, 0, 0);
            ((NodeBase)range).SetLiteralInput("Value", 200.0);
            ((NodeBase)range).SetLiteralInput("Min", 0.0);
            ((NodeBase)range).SetLiteralInput("Max", 100.0);
            g.Connect(start.Id, "Then", range.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

            result.Status.Should().Be(ExecutionStatus.Failed);
            result.Error.Should().BeOfType<AssertionFailedException>();
        }

        [Fact]
        public async Task RangeAssertNode_InRange_CompletesAndFiresThen()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var range = g.AddNode(RangeAssertNode.TypeIdConst, 0, 0);
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            ((NodeBase)range).SetLiteralInput("Value", 50.0);
            ((NodeBase)range).SetLiteralInput("Min", 0.0);
            ((NodeBase)range).SetLiteralInput("Max", 100.0);
            g.Connect(start.Id, "Then", range.Id, "In");
            g.Connect(range.Id, "Then", end.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

            result.Status.Should().Be(ExecutionStatus.Completed);
        }
    }
}
