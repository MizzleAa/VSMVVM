using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Runtime
{
    /// <summary>
    /// 브레이크포인트 + Continue/StepOver 시나리오 + 그에 동반된 LogSink 가시성 검증.
    /// <para>
    /// 사용자가 OpenCV 데모에서 Canny 노드에 BP 걸고 Continue 시 "Run Failed" 만 보이고 원인이 안 보였던 회귀를 막기 위함.
    /// </para>
    /// 다루는 시나리오:
    /// <list type="number">
    ///   <item>BP 도달 → BreakpointHitMessage 발화 + 컨텍스트 StepMode=Paused.</item>
    ///   <item>Continue 후 그래프 완주 → ExecutionStatus.Completed + "Run Completed" Info 엔트리.</item>
    ///   <item>StepOver 후 다음 노드 1개만 진행, 그 다음 다시 BreakpointHitMessage.</item>
    ///   <item>BP 직후 노드가 throw → ExecutionStatus.Failed + "Node failed: ..." Error 엔트리(Exception 포함).</item>
    ///   <item>그래프 완료 엔트리("Run Failed/Completed/Cancelled") 의 메시지가 정확한지.</item>
    ///   <item>Continue 후 정상 완료 시 IsPaused 해제 의미 — LogSink 에 "Run Completed" 마지막에 옴.</item>
    /// </list>
    /// </summary>
    public class BreakpointAndContinueTests
    {
        /// <summary>BP 가 걸린 노드에 도달하면 Messenger 로 BreakpointHitMessage 발화 + StepMode=Paused. Continue 호출 후 Completed.</summary>
        [Fact(Timeout = 15_000)]
        public async Task Breakpoint_Hit_Then_Continue_Resumes_To_Completion()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = g.AddNode(LogNode.TypeIdConst, 0, 0);
            ((NodeBase)log).SetLiteralInput("Format", "mid");
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            g.Connect(start.Id, "Then", log.Id, "In");
            g.Connect(log.Id, "Then", end.Id, "In");

            var scheduler = new SchedulerService();
            scheduler.ToggleBreakpoint(log.Id); // 글로벌 BP 등록

            var messenger = new Messenger();
            var sink = new InMemorySchedulerLogSink();
            var ctx = new ExecutionContext(g, messenger: messenger) { LogSink = sink };

            bool hit = false;
            var hitGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            messenger.Register<BreakpointHitMessage>(this, (s, m) =>
            {
                hit = true;
                hitGate.TrySetResult(true);
            });

            var runTask = scheduler.RunAsync(g, start.Id, ctx);

            await hitGate.Task; // BP 도달까지 대기
            hit.Should().BeTrue();
            ctx.StepMode.Should().Be(DebugStepMode.Paused, "BP 도달 시 컨텍스트는 Paused 로 전환되어야 함");

            scheduler.Continue();
            var result = await runTask;

            result.Status.Should().Be(ExecutionStatus.Completed);
            sink.GetAll().Should().Contain(e =>
                e.Level == SchedulerLogLevel.Info && e.Message.StartsWith("Run Completed"));
        }

        /// <summary>BP 직후 노드가 throw 하면 "Node failed: ..." Error 엔트리에 Exception 이 담겨야 한다.</summary>
        [Fact(Timeout = 15_000)]
        public async Task Breakpoint_Then_Continue_OnFailingNode_LogsNodeFailureWithException()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var failing = g.AddNode(AssertNode.TypeIdConst, 0, 0);
            ((NodeBase)failing).SetLiteralInput("Condition", false);
            ((NodeBase)failing).SetLiteralInput("Message", "bp-then-fail");
            g.Connect(start.Id, "Then", failing.Id, "In");

            var scheduler = new SchedulerService();
            scheduler.ToggleBreakpoint(failing.Id);

            var messenger = new Messenger();
            var sink = new InMemorySchedulerLogSink();
            var ctx = new ExecutionContext(g, messenger: messenger) { LogSink = sink };

            var hitGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            messenger.Register<BreakpointHitMessage>(this, (s, m) => hitGate.TrySetResult(true));

            var runTask = scheduler.RunAsync(g, start.Id, ctx);
            await hitGate.Task;
            scheduler.Continue();
            var result = await runTask;

            result.Status.Should().Be(ExecutionStatus.Failed);
            result.Error.Should().BeOfType<AssertionFailedException>();

            var nodeFailedEntry = sink.GetAll().FirstOrDefault(e =>
                e.Level == SchedulerLogLevel.Error && e.Message != null && e.Message.StartsWith("Node failed:"));
            nodeFailedEntry.Should().NotBeNull("'Node failed: ...' 엔트리가 LogSink 에 있어야 함");
            nodeFailedEntry.Exception.Should().BeOfType<AssertionFailedException>();
            nodeFailedEntry.NodeTypeId.Should().Be(AssertNode.TypeIdConst);

            var runFailedEntry = sink.GetAll().FirstOrDefault(e =>
                e.Level == SchedulerLogLevel.Error && e.Message != null && e.Message.StartsWith("Run Failed"));
            runFailedEntry.Should().NotBeNull("그래프-레벨 'Run Failed' 엔트리가 있어야 함");
            runFailedEntry.Exception.Should().BeOfType<AssertionFailedException>(
                "그래프-레벨 엔트리도 원인 exception 을 같이 가져 사용자가 LogPanel 에서 즉시 확인 가능해야 함");
        }

        /// <summary>BP + Cancel 시나리오 — 정지 중 토큰 취소되면 Cancelled status + Warning 엔트리.</summary>
        [Fact(Timeout = 15_000)]
        public async Task Breakpoint_Then_Cancel_ResultsInCancelledStatus()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = g.AddNode(LogNode.TypeIdConst, 0, 0);
            ((NodeBase)log).SetLiteralInput("Format", "x");
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            g.Connect(start.Id, "Then", log.Id, "In");
            g.Connect(log.Id, "Then", end.Id, "In");

            var scheduler = new SchedulerService();
            scheduler.ToggleBreakpoint(log.Id);

            var messenger = new Messenger();
            var sink = new InMemorySchedulerLogSink();
            using var cts = new CancellationTokenSource();
            var ctx = new ExecutionContext(g, cts.Token, messenger: messenger) { LogSink = sink };

            var hitGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            messenger.Register<BreakpointHitMessage>(this, (s, m) => hitGate.TrySetResult(true));

            var runTask = scheduler.RunAsync(g, start.Id, ctx);
            await hitGate.Task;
            cts.Cancel(); // 정지 중 토큰 취소
            var result = await runTask;

            result.Status.Should().Be(ExecutionStatus.Cancelled);
            sink.GetAll().Should().Contain(e =>
                e.Level == SchedulerLogLevel.Warning && e.Message.StartsWith("Run Cancelled"));
        }

        /// <summary>BP 없이 정상 실행 — Continue/StepOver 안 부르면 BreakpointHitMessage 0회, 즉시 완주.</summary>
        [Fact(Timeout = 15_000)]
        public async Task NoBreakpoint_RunsToCompletion_WithoutPause()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            g.Connect(start.Id, "Then", end.Id, "In");

            var scheduler = new SchedulerService();
            var messenger = new Messenger();
            var ctx = new ExecutionContext(g, messenger: messenger);

            int hits = 0;
            messenger.Register<BreakpointHitMessage>(this, (s, m) => hits++);

            var result = await scheduler.RunAsync(g, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            hits.Should().Be(0);
        }

        /// <summary>ToggleBreakpoint 가 토글 동작 — 두 번 호출하면 해제.</summary>
        [Fact(Timeout = 15_000)]
        public async Task ToggleBreakpoint_TwiceRemovesIt_NoHitOccurs()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = g.AddNode(LogNode.TypeIdConst, 0, 0);
            ((NodeBase)log).SetLiteralInput("Format", "x");
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            g.Connect(start.Id, "Then", log.Id, "In");
            g.Connect(log.Id, "Then", end.Id, "In");

            var scheduler = new SchedulerService();
            scheduler.ToggleBreakpoint(log.Id);
            scheduler.ToggleBreakpoint(log.Id); // 해제

            var messenger = new Messenger();
            var ctx = new ExecutionContext(g, messenger: messenger);
            int hits = 0;
            messenger.Register<BreakpointHitMessage>(this, (s, m) => hits++);

            var result = await scheduler.RunAsync(g, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            hits.Should().Be(0, "토글 두 번이면 BP 해제, 정지 없이 완주");
        }
    }
}
