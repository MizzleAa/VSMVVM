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
    public class NewBuiltInNodeTests
    {
        public NewBuiltInNodeTests()
        {
            BuiltInNodes.EnsureRegistered();
        }

        // === Constants ===

        [Fact]
        public async Task ConstantNode_Int_OutputsLiteralValue_ViaPull()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var c = (ConstantNode)g.AddNode(ConstantNode.TypeIdConst, 0, 0);
            c.ItemType = typeof(int);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            c.SetLiteralInput("Value", 42);
            ((NodeBase)output).SetLiteralInput("Key", "x");
            g.Connect(c.Id, "Out", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

            result.Status.Should().Be(ExecutionStatus.Completed);
            result.Outputs["x"].Should().Be(42);
        }

        [Fact]
        public async Task ConstantNode_Bool_FeedsBranch()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var c = (ConstantNode)g.AddNode(ConstantNode.TypeIdConst, 0, 0);
            c.ItemType = typeof(bool);
            var branch = g.AddNode(BranchNode.TypeIdConst, 0, 0);
            var passOutput = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            c.SetLiteralInput("Value", true);
            ((NodeBase)passOutput).SetLiteralInput("Key", "branch");
            ((NodeBase)passOutput).SetLiteralInput("Value", "TRUE");

            g.Connect(c.Id, "Out", branch.Id, "Condition");
            g.Connect(start.Id, "Then", branch.Id, "In");
            g.Connect(branch.Id, "True", passOutput.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

            result.Outputs.Should().ContainKey("branch");
            result.Outputs["branch"].Should().Be("TRUE");
        }

        // === Random ===

        [Fact]
        public async Task RandomIntNode_WithFixedSeed_ProducesDeterministicValue()
        {
            // 같은 seed + 같은 min/max → 같은 값.
            async Task<int> RunWithSeed(int seed)
            {
                var g = new NodeGraph();
                var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
                var r = g.AddNode(RandomIntNode.TypeIdConst, 0, 0);
                var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
                ((NodeBase)r).SetLiteralInput("Min", 0);
                ((NodeBase)r).SetLiteralInput("Max", 1_000_000);
                ((NodeBase)r).SetLiteralInput("Seed", seed);
                ((NodeBase)output).SetLiteralInput("Key", "v");
                g.Connect(r.Id, "Out", output.Id, "Value");
                g.Connect(start.Id, "Then", output.Id, "In");
                var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
                return (int)result.Outputs["v"];
            }

            var v1 = await RunWithSeed(42);
            var v2 = await RunWithSeed(42);
            v1.Should().Be(v2, "같은 seed 는 결정적 결과여야 함");

            var v3 = await RunWithSeed(7);
            v3.Should().NotBe(v1, "다른 seed 는 다른 결과여야 함");
        }

        [Fact]
        public async Task RandomDoubleNode_RangeIsRespected()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var r = g.AddNode(RandomDoubleNode.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)r).SetLiteralInput("Min", 10.0);
            ((NodeBase)r).SetLiteralInput("Max", 20.0);
            ((NodeBase)r).SetLiteralInput("Seed", 1);
            ((NodeBase)output).SetLiteralInput("Key", "v");
            g.Connect(r.Id, "Out", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            var v = (double)result.Outputs["v"];
            v.Should().BeGreaterOrEqualTo(10.0).And.BeLessOrEqualTo(20.0);
        }

        [Fact]
        public async Task RandomBoolNode_Probability1_AlwaysTrue()
        {
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var r = g.AddNode(RandomBoolNode.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)r).SetLiteralInput("Probability", 1.0);
            ((NodeBase)output).SetLiteralInput("Key", "v");
            g.Connect(r.Id, "Out", output.Id, "Value");
            g.Connect(start.Id, "Then", output.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));
            ((bool)result.Outputs["v"]).Should().BeTrue();
        }

        // === Toggle ===

        [Fact]
        public async Task ToggleNode_FlipsEachCall()
        {
            // Sequence(Then0/Then1/Then2) → Toggle 을 3번 호출하지만 같은 노드 인스턴스 → 데이터 캐시 동일 tick.
            // 호출 횟수별 다른 출력을 보려면 별도 그래프 인스턴스를 3번 실행하는 게 더 깔끔.
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var toggle = g.AddNode(ToggleNode.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)toggle).SetLiteralInput("InitialValue", false);
            ((NodeBase)output).SetLiteralInput("Key", "v");
            g.Connect(start.Id, "Then", toggle.Id, "In");
            g.Connect(toggle.Id, "Then", output.Id, "In");
            g.Connect(toggle.Id, "Value", output.Id, "Value");

            var ctx = new ExecutionContext(g);
            var r1 = await new SchedulerService().RunAsync(g, start.Id, ctx);
            r1.Outputs["v"].Should().Be(true, "InitialValue=false → 첫 호출 시 !false = true");

            // 같은 그래프 인스턴스 두 번째 실행 — toggle 상태 유지되어 false 로 뒤집힘.
            var ctx2 = new ExecutionContext(g);
            var r2 = await new SchedulerService().RunAsync(g, start.Id, ctx2);
            r2.Outputs["v"].Should().Be(false);
        }
    }
}
