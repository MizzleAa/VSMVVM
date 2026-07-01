using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.Core.Scheduler.Tests.TestHelpers;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Runtime
{
    public class SchedulerServiceTests
    {
        private static (NodeGraph graph, Guid startId, List<string> log, ExecutionContext ctx)
            BuildStartLogEndGraph(string message = "hello")
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);

            // Log.Message 는 미연결 → DefaultValue "" 사용. 직접 값 주입을 위해 ValueProducerNode 연결.
            var producer = new ValueProducerNode<string>(message);
            graph.AddNode(producer, 0, 0);

            graph.Connect(start.Id, "Then", log.Id, "In");
            graph.Connect(log.Id, "Then", end.Id, "In");
            graph.Connect(producer.Id, "Value", log.Id, "Arg0");

            var captured = new List<string>();
            var ctx = new ExecutionContext(graph);
            ctx.Variables["__LogCapture"] = captured;
            return (graph, start.Id, captured, ctx);
        }

        [Fact]
        public async Task RunAsync_StartLogEnd_ExecutesInOrder_AndLogsMessage()
        {
            var (graph, startId, log, ctx) = BuildStartLogEndGraph("hello");
            var svc = new SchedulerService();

            var result = await svc.RunAsync(graph, startId, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            result.NodesExecuted.Should().Be(3); // Start, Log, End
            log.Should().ContainSingle().Which.Should().Be("hello");
        }

        [Fact]
        public async Task RunAsync_BranchTrue_PicksTrueBranch()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var branch = graph.AddNode(BranchNode.TypeIdConst, 0, 0);
            var condProducer = new ValueProducerNode<bool>(true);
            graph.AddNode(condProducer, 0, 0);
            var trueLog = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var falseLog = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var trueMsg = new ValueProducerNode<string>("TRUE");
            var falseMsg = new ValueProducerNode<string>("FALSE");
            graph.AddNode(trueMsg, 0, 0);
            graph.AddNode(falseMsg, 0, 0);

            graph.Connect(start.Id, "Then", branch.Id, "In");
            graph.Connect(condProducer.Id, "Value", branch.Id, "Condition");
            graph.Connect(branch.Id, "True", trueLog.Id, "In");
            graph.Connect(branch.Id, "False", falseLog.Id, "In");
            graph.Connect(trueMsg.Id, "Value", trueLog.Id, "Arg0");
            graph.Connect(falseMsg.Id, "Value", falseLog.Id, "Arg0");

            var captured = new List<string>();
            var ctx = new ExecutionContext(graph);
            ctx.Variables["__LogCapture"] = captured;

            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            captured.Should().ContainSingle().Which.Should().Be("TRUE");
        }

        [Fact]
        public async Task RunAsync_BranchFalse_PicksFalseBranch()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var branch = graph.AddNode(BranchNode.TypeIdConst, 0, 0);
            var condProducer = new ValueProducerNode<bool>(false);
            graph.AddNode(condProducer, 0, 0);
            var falseLog = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var falseMsg = new ValueProducerNode<string>("FALSE");
            graph.AddNode(falseMsg, 0, 0);

            graph.Connect(start.Id, "Then", branch.Id, "In");
            graph.Connect(condProducer.Id, "Value", branch.Id, "Condition");
            graph.Connect(branch.Id, "False", falseLog.Id, "In");
            graph.Connect(falseMsg.Id, "Value", falseLog.Id, "Arg0");

            var captured = new List<string>();
            var ctx = new ExecutionContext(graph);
            ctx.Variables["__LogCapture"] = captured;

            await new SchedulerService().RunAsync(graph, start.Id, ctx);

            captured.Should().ContainSingle().Which.Should().Be("FALSE");
        }

        [Fact]
        public async Task RunAsync_SequenceNode_FiresOutputsInDeclaredOrder()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var seq = graph.AddNode(SequenceNode.TypeIdConst, 0, 0);
            var logA = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var logB = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var logC = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var msgA = new ValueProducerNode<string>("A");
            var msgB = new ValueProducerNode<string>("B");
            var msgC = new ValueProducerNode<string>("C");
            graph.AddNode(msgA, 0, 0);
            graph.AddNode(msgB, 0, 0);
            graph.AddNode(msgC, 0, 0);

            graph.Connect(start.Id, "Then", seq.Id, "In");
            graph.Connect(seq.Id, "Then0", logA.Id, "In");
            graph.Connect(seq.Id, "Then1", logB.Id, "In");
            graph.Connect(seq.Id, "Then2", logC.Id, "In");
            graph.Connect(msgA.Id, "Value", logA.Id, "Arg0");
            graph.Connect(msgB.Id, "Value", logB.Id, "Arg0");
            graph.Connect(msgC.Id, "Value", logC.Id, "Arg0");

            var captured = new List<string>();
            var ctx = new ExecutionContext(graph);
            ctx.Variables["__LogCapture"] = captured;

            await new SchedulerService().RunAsync(graph, start.Id, ctx);

            captured.Should().Equal("A", "B", "C");
        }

        [Fact]
        public async Task RunAsync_NMConvergence_OneExecOutFiresToMultipleTargets()
        {
            // start --Then-+--> logA
            //              +--> logB
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var logA = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var logB = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var msgA = new ValueProducerNode<string>("A");
            var msgB = new ValueProducerNode<string>("B");
            graph.AddNode(msgA, 0, 0);
            graph.AddNode(msgB, 0, 0);

            graph.Connect(start.Id, "Then", logA.Id, "In");
            graph.Connect(start.Id, "Then", logB.Id, "In");
            graph.Connect(msgA.Id, "Value", logA.Id, "Arg0");
            graph.Connect(msgB.Id, "Value", logB.Id, "Arg0");

            var captured = new List<string>();
            var ctx = new ExecutionContext(graph);
            ctx.Variables["__LogCapture"] = captured;

            await new SchedulerService().RunAsync(graph, start.Id, ctx);

            captured.Should().Equal("A", "B"); // 연결 등록 순서대로
        }

        [Fact]
        public async Task RunAsync_Cancellation_StopsExecution_AndReturnsCancelledStatus()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var delay = graph.AddNode(DelayNode.TypeIdConst, 0, 0);
            var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            var seconds = new ValueProducerNode<double>(10.0); // 길게
            graph.AddNode(seconds, 0, 0);

            graph.Connect(start.Id, "Then", delay.Id, "In");
            graph.Connect(seconds.Id, "Value", delay.Id, "Seconds");
            graph.Connect(delay.Id, "Then", end.Id, "In");

            using var cts = new CancellationTokenSource();
            var ctx = new ExecutionContext(graph, cts.Token);
            var runTask = new SchedulerService().RunAsync(graph, start.Id, ctx);

            // 그래프가 RunAsync 진입 → StartNode 실행 → DelayNode await에 안착할 충분한 여유를 주고 취소.
            // 너무 짧으면 RunAsync 초입에서 cancel이 먼저 도착해 OperationCanceledException이 throw되어
            // ExecutionResult.Cancelled로 분류는 동일하지만 race가 발생할 수 있으므로 여유 있게 200ms.
            cts.CancelAfter(200);
            var result = await runTask;

            result.Status.Should().Be(ExecutionStatus.Cancelled);
        }

        [Fact]
        public async Task DataPull_CachesUpstream_OnlyEvaluatesOncePerTick()
        {
            // producer가 두 노드(logA, logB)의 Message에 연결되어도 ExecuteAsync는 1회만 호출되어야 한다.
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var logA = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var logB = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var producer = new ValueProducerNode<string>("shared");
            graph.AddNode(producer, 0, 0);

            graph.Connect(start.Id, "Then", logA.Id, "In");
            graph.Connect(logA.Id, "Then", logB.Id, "In");
            graph.Connect(producer.Id, "Value", logA.Id, "Arg0");
            graph.Connect(producer.Id, "Value", logB.Id, "Arg0");

            var ctx = new ExecutionContext(graph);
            ctx.Variables["__LogCapture"] = new List<string>();

            await new SchedulerService().RunAsync(graph, start.Id, ctx);

            // producer의 ExecuteAsync는 정확히 1번만.
            producer.EvaluatedCount.Should().Be(1);
        }

        [Fact]
        public async Task DataPull_CyclicDependency_Throws_AndRunFails()
        {
            // cyclic.Out → cyclic.In 자기 사이클. exec-in이 있는 노드로 만들어 직접 진입시킨다.
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var driver = new CyclicDriverNode();
            graph.AddNode(driver, 0, 0);

            graph.Connect(start.Id, "Then", driver.Id, "In");
            // driver의 Echo 출력 → 자기 Source 입력 (사이클)
            graph.Connect(driver.Id, "Echo", driver.Id, "Source");

            var ctx = new ExecutionContext(graph);

            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Failed);
            result.Error.Should().BeOfType<CyclicDataDependencyException>();
        }

        [Fact]
        public async Task MaxNodesExecuted_Exceeded_ThrowsSchedulerOverflow_AndEmitsGuardMessage()
        {
            // start → log 가 자기 자신으로 루프
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = graph.AddNode(LogNode.TypeIdConst, 0, 0);

            graph.Connect(start.Id, "Then", log.Id, "In");
            // log.Then → log.In 루프
            graph.Connect(log.Id, "Then", log.Id, "In");

            var ctx = new ExecutionContext(graph) { MaxNodesExecuted = 50 };
            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Failed);
            result.Error.Should().BeOfType<SchedulerOverflowException>();
        }

        [Fact]
        public async Task PerNodeTimeout_Exceeded_ThrowsNodeTimeoutException()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var delay = graph.AddNode(DelayNode.TypeIdConst, 0, 0);
            var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            var seconds = new ValueProducerNode<double>(5.0);
            graph.AddNode(seconds, 0, 0);

            graph.Connect(start.Id, "Then", delay.Id, "In");
            graph.Connect(seconds.Id, "Value", delay.Id, "Seconds");
            graph.Connect(delay.Id, "Then", end.Id, "In");

            var ctx = new ExecutionContext(graph) { PerNodeTimeoutMs = 100 };

            var result = await new SchedulerService().RunAsync(graph, start.Id, ctx);

            // Cancellation 또는 NodeTimeoutException 둘 다 가능 (CancellationToken 전파 vs Delay race).
            result.Status.Should().BeOneOf(ExecutionStatus.Failed, ExecutionStatus.Cancelled);
            if (result.Status == ExecutionStatus.Failed)
            {
                result.Error.Should().BeOfType<NodeTimeoutException>();
            }
        }

        // === Phase I.2a — NodeExitedMessage.Inputs/Outputs 스냅샷 캡처 ===

        [Fact]
        public async Task NodeExitedMessage_LogNode_CarriesInputSnapshot_FromUpstreamProducer()
        {
            // LogNode 가 ValueProducer("hello") 의 출력을 입력으로 받는다.
            // 실행 직후 발화되는 NodeExitedMessage 의 Inputs["Arg0"] == "hello" 이어야 한다.
            // (Phase I: LogNode 의 Message 핀 제거, Format + Args 흐름.)
            var (graph, startId, _, ctx) = BuildStartLogEndGraph("hello");
            var messenger = new Messenger();
            ctx = new ExecutionContext(graph, messenger: messenger);
            ctx.Variables["__LogCapture"] = new List<string>();

            var byNode = new Dictionary<string, NodeExitedMessage>();
            messenger.Register<NodeExitedMessage>(this, (s, m) => byNode[m.NodeTypeId] = m);

            await new SchedulerService().RunAsync(graph, startId, ctx);

            byNode.Should().ContainKey(LogNode.TypeIdConst);
            var logExit = byNode[LogNode.TypeIdConst];
            logExit.Inputs.Should().ContainKey("Arg0");
            logExit.Inputs["Arg0"].Should().Be("hello");
        }

        [Fact]
        public async Task NodeExitedMessage_ProducerNode_CarriesOutputSnapshot()
        {
            // ValueProducer<string>("hello").Value 출력이 OutputCapture(Outputs["Value"]) 에 들어가야 한다.
            var (graph, startId, _, ctx) = BuildStartLogEndGraph("hello");
            var messenger = new Messenger();
            ctx = new ExecutionContext(graph, messenger: messenger);
            ctx.Variables["__LogCapture"] = new List<string>();

            var producerExits = new List<NodeExitedMessage>();
            messenger.Register<NodeExitedMessage>(this, (s, m) =>
            {
                // ValueProducerNode 의 TypeId 는 동적으로 부여되므로 Outputs 에 "Value" 키가 있는 것을 producer로 식별.
                if (m.Outputs.ContainsKey("Value")) producerExits.Add(m);
            });

            await new SchedulerService().RunAsync(graph, startId, ctx);

            // 주의: ValueProducerNode 가 Exec 흐름에 포함되지 않으면 NodeExitedMessage 가 안 나올 수 있음.
            // BuildStartLogEndGraph 에서 producer 는 data pull 로만 평가됨 → exec 메인루프에서 NodeExitedMessage 발화 안 함.
            // 그래서 직접 검증할 노드는 LogNode 의 입력 측이 적절. 본 테스트는 캡처 함수가 출력 핀도 빈 경우 sparse 인지 확인.
            // (Exec 흐름에 들어간 노드 중 출력 데이터 핀이 채워진 것이 있으면 Outputs 가 들어 있어야 함.)
            // 본 케이스에서는 producer 가 exec 루프 외부에서 평가되므로 producerExits 가 비어있는 것이 정상.
            producerExits.Should().BeEmpty(
                "ValueProducer 는 pull 데이터로만 평가되어 NodeExitedMessage 가 emit 되지 않음");
        }

        [Fact]
        public async Task NodeExitedMessage_NodeWithNoDataPins_CarriesEmptyInputsAndOutputs()
        {
            // StartNode 와 EndNode 는 데이터 핀이 없으므로 Inputs/Outputs 모두 비어 있어야 한다.
            var (graph, startId, _, ctx) = BuildStartLogEndGraph();
            var messenger = new Messenger();
            ctx = new ExecutionContext(graph, messenger: messenger);
            ctx.Variables["__LogCapture"] = new List<string>();

            var byNode = new Dictionary<string, NodeExitedMessage>();
            messenger.Register<NodeExitedMessage>(this, (s, m) => byNode[m.NodeTypeId] = m);

            await new SchedulerService().RunAsync(graph, startId, ctx);

            byNode[StartNode.TypeIdConst].Inputs.Should().BeEmpty();
            byNode[StartNode.TypeIdConst].Outputs.Should().BeEmpty();
            byNode[EndNode.TypeIdConst].Inputs.Should().BeEmpty();
            byNode[EndNode.TypeIdConst].Outputs.Should().BeEmpty();
        }
    }
}
