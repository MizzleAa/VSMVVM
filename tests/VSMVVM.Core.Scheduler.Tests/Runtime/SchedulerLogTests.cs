using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Runtime
{
    public class SchedulerLogTests
    {
        [Fact]
        public async Task RunAsync_WritesRunStartAndCompletion_ToLogSink()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            g.Connect(start.Id, "Then", end.Id, "In");

            var sink = new InMemorySchedulerLogSink();
            var ctx = new ExecutionContext(g) { LogSink = sink };

            await new SchedulerService().RunAsync(g, start.Id, ctx);

            var all = sink.GetAll();
            all.Should().Contain(e => e.Message.StartsWith("Run started") && e.Level == SchedulerLogLevel.Info);
            all.Should().Contain(e => e.Message.Contains("Completed") && e.Level == SchedulerLogLevel.Info);
            all.Should().Contain(e => e.NodeTypeId == StartNode.TypeIdConst);
        }

        [Fact]
        public async Task FailingNode_WritesErrorLog_WithException()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var assert = g.AddNode(AssertNode.TypeIdConst, 0, 0);
            ((VSMVVM.Core.Scheduler.Nodes.NodeBase)assert).SetLiteralInput("Condition", false);
            ((VSMVVM.Core.Scheduler.Nodes.NodeBase)assert).SetLiteralInput("Message", "boom");
            g.Connect(start.Id, "Then", assert.Id, "In");

            var sink = new InMemorySchedulerLogSink();
            var ctx = new ExecutionContext(g) { LogSink = sink };

            var result = await new SchedulerService().RunAsync(g, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Failed);
            result.Error.Should().BeOfType<AssertionFailedException>();

            var errorEntries = sink.GetAll().Where(e => e.Level == SchedulerLogLevel.Error).ToList();
            errorEntries.Should().NotBeEmpty();
            errorEntries.Should().Contain(e => e.Exception is AssertionFailedException);
        }

        [Fact]
        public async Task FailingNode_RunFailedEntry_AlsoCarriesException()
        {
            // catch (Exception) 경로에서 그래프 완료 로그("Run Failed ... ") 도 Exception 을 같이 담아야 한다.
            // SchedulerLogPanel 이 exception 도 보여주기 때문에 사용자가 패널만 봐도 원인 파악 가능.
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var assert = g.AddNode(AssertNode.TypeIdConst, 0, 0);
            ((VSMVVM.Core.Scheduler.Nodes.NodeBase)assert).SetLiteralInput("Condition", false);
            ((VSMVVM.Core.Scheduler.Nodes.NodeBase)assert).SetLiteralInput("Message", "diag-boom");
            g.Connect(start.Id, "Then", assert.Id, "In");

            var sink = new InMemorySchedulerLogSink();
            var ctx = new ExecutionContext(g) { LogSink = sink };
            await new SchedulerService().RunAsync(g, start.Id, ctx);

            // "Run Failed" 로 끝나는 graph-level 엔트리에도 Exception 이 담겨야 함.
            var failedEntry = sink.GetAll().FirstOrDefault(e =>
                e.Level == SchedulerLogLevel.Error && e.Message != null && e.Message.StartsWith("Run Failed"));
            failedEntry.Should().NotBeNull("'Run Failed' graph-level 엔트리가 있어야 함");
            failedEntry.Exception.Should().BeOfType<AssertionFailedException>(
                "graph-level 실패 엔트리도 원인 exception 을 같이 가져 사용자가 패널에서 즉시 확인 가능해야 함");
        }

        [Fact]
        public async Task LogNode_WritesMessage_ToLogSink_NotJustLogger()
        {
            // LogNode 의 Message 입력이 LogSink 에도 기록되어야 한다 — 사용자가 SchedulerLogPanel 에서
            // 자기 로그를 볼 수 있어야 함. (이전엔 ILoggerService 채널로만 가서 패널에 안 보였음.)
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = g.AddNode(LogNode.TypeIdConst, 0, 0);
            ((VSMVVM.Core.Scheduler.Nodes.NodeBase)log).SetLiteralInput("Format", "Sample");
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            g.Connect(start.Id, "Then", log.Id, "In");
            g.Connect(log.Id, "Then", end.Id, "In");

            var sink = new InMemorySchedulerLogSink();
            var ctx = new ExecutionContext(g) { LogSink = sink };

            var result = await new SchedulerService().RunAsync(g, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            sink.GetAll().Should().Contain(e =>
                e.Message == "Sample" && e.NodeTypeId == LogNode.TypeIdConst && e.Level == SchedulerLogLevel.Info,
                "LogNode 의 Message 가 LogSink 에 Info 로 기록되어야 함");
        }

        [Fact]
        public void InMemorySchedulerLogSink_EnforcesCapacity()
        {
            var sink = new InMemorySchedulerLogSink(capacity: 2);
            for (int i = 0; i < 5; i++)
            {
                sink.Write(new SchedulerLogEntry(
                    DateTimeOffset.UtcNow, SchedulerLogLevel.Info,
                    Guid.NewGuid(), null, null, $"msg{i}", null));
            }
            sink.GetAll().Should().HaveCount(2);
            sink.GetAll().Last().Message.Should().Be("msg4");
        }
    }
}
