using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.Core.Scheduler.Tests.TestHelpers;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Runtime
{
    public class ExecutionRunTests
    {
        private static (NodeGraph g, Guid startId, ExecutionContext ctx, InMemoryExecutionHistoryStore store)
            BuildSimpleGraphWithStore()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = g.AddNode(LogNode.TypeIdConst, 0, 0);
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            g.Connect(start.Id, "Then", log.Id, "In");
            g.Connect(log.Id, "Then", end.Id, "In");

            var store = new InMemoryExecutionHistoryStore(capacity: 3);
            var ctx = new ExecutionContext(g)
            {
                HistoryStore = store,
            };
            ctx.Variables["__LogCapture"] = new List<string>();
            return (g, start.Id, ctx, store);
        }

        [Fact]
        public async Task RunAsync_PushesRunToHistoryStore_OnCompletion()
        {
            var (g, start, ctx, store) = BuildSimpleGraphWithStore();
            store.GetAll().Should().BeEmpty();

            await new SchedulerService().RunAsync(g, start, ctx);

            store.GetAll().Should().HaveCount(1);
            var run = store.GetAll()[0];
            run.Status.Should().Be(ExecutionStatus.Completed);
            run.Records.Should().HaveCount(3); // Start, Log, End
            run.Records[0].TypeId.Should().Be(StartNode.TypeIdConst);
        }

        [Fact]
        public async Task ExecutionRun_RecordCarriesInputsAndOutputs_AndElapsed()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = g.AddNode(LogNode.TypeIdConst, 0, 0);
            var producer = new ValueProducerNode<string>("hi");
            g.AddNode(producer, 0, 0);
            g.Connect(start.Id, "Then", log.Id, "In");
            g.Connect(producer.Id, "Value", log.Id, "Arg0");

            var store = new InMemoryExecutionHistoryStore();
            var ctx = new ExecutionContext(g) { HistoryStore = store };
            ctx.Variables["__LogCapture"] = new List<string>();

            await new SchedulerService().RunAsync(g, start.Id, ctx);

            var run = store.GetAll().Single();
            var logRec = run.Records.Single(r => r.TypeId == LogNode.TypeIdConst);
            logRec.InputSnapshot.Should().ContainKey("Arg0");
            logRec.InputSnapshot["Arg0"].Should().Be("hi");
            logRec.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
            logRec.Success.Should().BeTrue();
        }

        [Fact]
        public void InMemoryExecutionHistoryStore_RaisesRunAddedEvent_AndEnforcesCapacity()
        {
            var store = new InMemoryExecutionHistoryStore(capacity: 2);
            var added = new List<ExecutionRun>();
            store.RunAdded += (_, r) => added.Add(r);

            var r1 = new ExecutionRun(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
            var r2 = new ExecutionRun(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
            var r3 = new ExecutionRun(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
            store.Add(r1);
            store.Add(r2);
            store.Add(r3);

            added.Should().HaveCount(3);
            store.GetAll().Should().BeEquivalentTo(new[] { r2, r3 },
                "LRU: capacity 2 → 가장 오래된 r1 제거");
        }

        [Fact]
        public async Task ExecutionResult_Outputs_ReflectsOutputNode()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);

            // Key="sum", Value=42 (리터럴 입력)
            ((VSMVVM.Core.Scheduler.Nodes.NodeBase)output).SetLiteralInput("Key", "sum");
            ((VSMVVM.Core.Scheduler.Nodes.NodeBase)output).SetLiteralInput("Value", 42);

            g.Connect(start.Id, "Then", output.Id, "In");
            g.Connect(output.Id, "Then", end.Id, "In");

            var result = await new SchedulerService().RunAsync(g, start.Id, new ExecutionContext(g));

            result.Status.Should().Be(ExecutionStatus.Completed);
            result.Outputs.Should().ContainKey("sum");
            result.Outputs["sum"].Should().Be(42);
        }
    }
}
