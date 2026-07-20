using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.Core.Scheduler.Tests.TestHelpers;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Runtime
{
    /// <summary>
    /// ForkNode 병렬 분기 · JoinNode 재합류 회로의 안전성 검증.
    /// - Fork 두 브랜치가 실제로 병렬 실행 (즉 브랜치들의 total elapsed &lt; 두 브랜치 순차합)
    /// - Join 은 마지막 도착자만 downstream 을 이어감 (Then 뒤 노드는 정확히 1 번 실행)
    /// - 브랜치 하나에서 예외 발생 시 그래프가 Failed 로 종결
    /// - 기존 직렬 그래프 회귀 없음
    /// </summary>
    [Trait("Category", "Stress")]
    public class ForkJoinNodeTests
    {
        [Fact]
        public async Task Fork_TwoBranches_ExecuteInParallel()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var fork = graph.AddNode(ForkNode.TypeIdConst, 0, 0);
            var join = graph.AddNode(JoinNode.TypeIdConst, 0, 0);
            var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);

            var a = new DelayMarkerNode("A", TimeSpan.FromMilliseconds(150));
            var b = new DelayMarkerNode("B", TimeSpan.FromMilliseconds(150));
            graph.AddNode(a, 0, 0);
            graph.AddNode(b, 0, 0);

            graph.Connect(start.Id, "Then", fork.Id, "In");
            graph.Connect(fork.Id, "Branch0", a.Id, "In");
            graph.Connect(fork.Id, "Branch1", b.Id, "In");
            graph.Connect(a.Id, "Then", join.Id, "In0");
            graph.Connect(b.Id, "Then", join.Id, "In1");
            graph.Connect(join.Id, "Then", end.Id, "In");

            var ctx = new ExecutionContext(graph);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);
            sw.Stop();

            result.Status.Should().Be(ExecutionStatus.Completed);
            // 각 브랜치가 150ms 슬립. 병렬이면 총 elapsed 는 300ms 미만 (여유 있게 250ms 로 검증).
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(250),
                because: "Fork 브랜치가 실제 병렬 실행되어야 함");
            DelayMarkerNode.RecordedNames.Should().Contain(new[] { "A", "B" });
        }

        [Fact]
        public async Task Join_LastArrival_FiresThenExactlyOnce()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var fork = graph.AddNode(ForkNode.TypeIdConst, 0, 0);
            var join = graph.AddNode(JoinNode.TypeIdConst, 0, 0);

            var a = new DelayMarkerNode("A", TimeSpan.FromMilliseconds(30));
            var b = new DelayMarkerNode("B", TimeSpan.FromMilliseconds(80));
            var afterJoin = new DelayMarkerNode("AfterJoin", TimeSpan.Zero);
            graph.AddNode(a, 0, 0);
            graph.AddNode(b, 0, 0);
            graph.AddNode(afterJoin, 0, 0);

            graph.Connect(start.Id, "Then", fork.Id, "In");
            graph.Connect(fork.Id, "Branch0", a.Id, "In");
            graph.Connect(fork.Id, "Branch1", b.Id, "In");
            graph.Connect(a.Id, "Then", join.Id, "In0");
            graph.Connect(b.Id, "Then", join.Id, "In1");
            graph.Connect(join.Id, "Then", afterJoin.Id, "In");

            var ctx = new ExecutionContext(graph);
            DelayMarkerNode.ResetCounters();
            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            DelayMarkerNode.ExecutionCount("AfterJoin").Should().Be(1,
                because: "Join 뒤 downstream 은 마지막 도달자에서 정확히 1회만 실행되어야 함");
        }

        [Fact]
        public async Task Fork_BranchException_MakesRunFailed()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var fork = graph.AddNode(ForkNode.TypeIdConst, 0, 0);
            var join = graph.AddNode(JoinNode.TypeIdConst, 0, 0);

            var ok = new DelayMarkerNode("Ok", TimeSpan.FromMilliseconds(20));
            var bad = new ThrowingNode("boom");
            var afterJoin = new DelayMarkerNode("AfterJoin", TimeSpan.Zero);
            graph.AddNode(ok, 0, 0);
            graph.AddNode(bad, 0, 0);
            graph.AddNode(afterJoin, 0, 0);

            graph.Connect(start.Id, "Then", fork.Id, "In");
            graph.Connect(fork.Id, "Branch0", ok.Id, "In");
            graph.Connect(fork.Id, "Branch1", bad.Id, "In");
            graph.Connect(ok.Id, "Then", join.Id, "In0");
            graph.Connect(bad.Id, "Then", join.Id, "In1");
            graph.Connect(join.Id, "Then", afterJoin.Id, "In");

            var ctx = new ExecutionContext(graph);
            DelayMarkerNode.ResetCounters();
            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Failed);
            DelayMarkerNode.ExecutionCount("AfterJoin").Should().Be(0,
                because: "브랜치 예외 시 downstream 은 실행되지 않아야 함");
        }

        [Fact]
        public async Task Fork_TwoBranches_EachPullsLiterals_NoRace()
        {
            // 두 브랜치가 각자 LogNode 를 실행하며 Format 리터럴을 pull 하는 상황.
            // ExecutionContext._dataCache 가 Dictionary 였을 때 Sample 데모에서 재현된 race 회귀 방지.
            // sink 는 스레드 안전한 InMemorySchedulerLogSink 사용 — LogNode 의 __LogCapture (List<string>)
            // 는 스레드 안전 아니라 병렬 브랜치가 동시 Add 시 자체 race 발생.
            BuiltInNodes.EnsureRegistered();

            for (int iteration = 0; iteration < 100; iteration++)
            {
                var graph = new NodeGraph();
                var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
                var fork = graph.AddNode(ForkNode.TypeIdConst, 0, 0);
                var join = graph.AddNode(JoinNode.TypeIdConst, 0, 0);

                var logA = graph.AddNode(LogNode.TypeIdConst, 0, 0);
                ((NodeBase)logA).SetLiteralInput("Format", "Branch A done");
                var logB = graph.AddNode(LogNode.TypeIdConst, 0, 0);
                ((NodeBase)logB).SetLiteralInput("Format", "Branch B done");

                graph.Connect(start.Id, "Then", fork.Id, "In");
                graph.Connect(fork.Id, "Branch0", logA.Id, "In");
                graph.Connect(fork.Id, "Branch1", logB.Id, "In");
                graph.Connect(logA.Id, "Then", join.Id, "In0");
                graph.Connect(logB.Id, "Then", join.Id, "In1");

                var ctx = new ExecutionContext(graph);
                var sink = new InMemorySchedulerLogSink();
                ctx.LogSink = sink;

                var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

                result.Status.Should().Be(ExecutionStatus.Completed,
                    because: $"iteration {iteration}: race 로 IndexOutOfRangeException 등이 발생하면 안 됨");
                var messages = sink.GetAll()
                    .Where(e => e.Level == SchedulerLogLevel.Info)
                    .Select(e => e.Message)
                    .ToList();
                messages.Should().Contain("Branch A done").And.Contain("Branch B done");
            }
        }

        [Fact]
        public async Task Fork_TwoBranches_ShareUpstream_EvaluatesOnce()
        {
            // 두 브랜치가 같은 상류 데이터 노드 (ValueProducerNode) 를 pull 하는 상황.
            // per-node evaluate lock 이 없으면 EvaluatedCount 가 2 로 튀거나 (double-eval) race 발생 가능.
            BuiltInNodes.EnsureRegistered();

            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var fork = graph.AddNode(ForkNode.TypeIdConst, 0, 0);
            var join = graph.AddNode(JoinNode.TypeIdConst, 0, 0);

            var shared = new ValueProducerNode<string>("shared-value");
            graph.AddNode(shared, 0, 0);

            var logA = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            ((NodeBase)logA).SetLiteralInput("Format", "{0}");
            var logB = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            ((NodeBase)logB).SetLiteralInput("Format", "{0}");

            graph.Connect(start.Id, "Then", fork.Id, "In");
            graph.Connect(fork.Id, "Branch0", logA.Id, "In");
            graph.Connect(fork.Id, "Branch1", logB.Id, "In");
            graph.Connect(shared.Id, "Value", logA.Id, "Arg0");
            graph.Connect(shared.Id, "Value", logB.Id, "Arg0");
            graph.Connect(logA.Id, "Then", join.Id, "In0");
            graph.Connect(logB.Id, "Then", join.Id, "In1");

            var ctx = new ExecutionContext(graph);
            var sink = new InMemorySchedulerLogSink();
            ctx.LogSink = sink;

            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            shared.EvaluatedCount.Should().Be(1,
                because: "공유 상류 노드는 per-node evaluate lock 으로 정확히 1 회만 실행되어야 함");
            // LogNode 만 Info 레벨로 msg 를 남긴다. SchedulerService 의 노드 진입/종료 debug 는 제외.
            var messages = sink.GetAll()
                .Where(e => e.Level == SchedulerLogLevel.Info
                            && (e.NodeId == logA.Id || e.NodeId == logB.Id))
                .Select(e => e.Message)
                .ToList();
            messages.Should().OnlyContain(s => s == "shared-value").And.HaveCount(2);
        }

        [Fact]
        public async Task Sequence_MultipleFiredPins_StaysSerial_NoRegression()
        {
            // SequenceNode 는 Fork 가 아니므로 다중 발화지만 여전히 직렬 실행. 기존 회귀 방지.
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var seq = graph.AddNode(SequenceNode.TypeIdConst, 0, 0);
            var a = new OrderTrackingNode("A");
            var b = new OrderTrackingNode("B");
            var c = new OrderTrackingNode("C");
            graph.AddNode(a, 0, 0);
            graph.AddNode(b, 0, 0);
            graph.AddNode(c, 0, 0);

            graph.Connect(start.Id, "Then", seq.Id, "In");
            graph.Connect(seq.Id, "Then0", a.Id, "In");
            graph.Connect(seq.Id, "Then1", b.Id, "In");
            graph.Connect(seq.Id, "Then2", c.Id, "In");

            OrderTrackingNode.Reset();
            var ctx = new ExecutionContext(graph);
            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            OrderTrackingNode.Order.Should().Equal("A", "B", "C");
        }

        // ============= 테스트 도우미 노드 =============

        private sealed class DelayMarkerNode : NodeBase
        {
            public const string TypeIdConst = "Tests.DelayMarker";
            public override string TypeId => TypeIdConst;

            private static readonly ConcurrentDictionary<string, int> _counters
                = new ConcurrentDictionary<string, int>();
            private static readonly ConcurrentBag<string> _names = new ConcurrentBag<string>();

            public static IReadOnlyCollection<string> RecordedNames => _names;
            public static int ExecutionCount(string name) => _counters.TryGetValue(name, out var c) ? c : 0;
            public static void ResetCounters()
            {
                _counters.Clear();
                while (_names.TryTake(out _)) { }
            }

            private readonly string _name;
            private readonly TimeSpan _delay;
            public DelayMarkerNode(string name, TimeSpan delay) { _name = name; _delay = delay; }

            private static readonly PinDescriptor[] PinSpec = new[]
            {
                new PinDescriptor("In",   "In",   PinDirection.Input,  PinKind.Exec, typeof(void), null),
                new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void), null),
            };
            protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

            public override async Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
            {
                if (_delay > TimeSpan.Zero) await Task.Delay(_delay, context.CancellationToken).ConfigureAwait(false);
                _names.Add(_name);
                _counters.AddOrUpdate(_name, 1, (_, c) => c + 1);
                return ExecutionFlow.Continue("Then");
            }
        }

        private sealed class ThrowingNode : NodeBase
        {
            public const string TypeIdConst = "Tests.Throwing";
            public override string TypeId => TypeIdConst;
            private readonly string _message;
            public ThrowingNode(string message) { _message = message; }

            private static readonly PinDescriptor[] PinSpec = new[]
            {
                new PinDescriptor("In",   "In",   PinDirection.Input,  PinKind.Exec, typeof(void), null),
                new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void), null),
            };
            protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

            public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
                => throw new InvalidOperationException(_message);
        }

        private sealed class OrderTrackingNode : NodeBase
        {
            public const string TypeIdConst = "Tests.OrderTracking";
            public override string TypeId => TypeIdConst;

            private static readonly List<string> _order = new List<string>();
            private static readonly object _lock = new object();
            public static IReadOnlyList<string> Order { get { lock (_lock) return _order.ToArray(); } }
            public static void Reset() { lock (_lock) _order.Clear(); }

            private readonly string _name;
            public OrderTrackingNode(string name) { _name = name; }

            private static readonly PinDescriptor[] PinSpec = new[]
            {
                new PinDescriptor("In",   "In",   PinDirection.Input,  PinKind.Exec, typeof(void), null),
                new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void), null),
            };
            protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

            public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
            {
                lock (_lock) _order.Add(_name);
                return Task.FromResult(ExecutionFlow.Continue("Then"));
            }
        }
    }
}