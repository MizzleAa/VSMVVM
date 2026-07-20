using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.WPF.Scheduler.Tests
{
    /// <summary>
    /// 회귀 방지: 사용자가 무거운 동기 CPU 작업을 노드에 둬도 UI 가 멈춰서는 안 된다.
    /// <para>
    /// 시나리오 (사용자 보고): OpenCV CreateGradient(10000) 같은 1억회 루프 — 동기 CPU 작업이
    /// SchedulerService.RunAsync 안에서 직접 await 되어 호출 스레드(보통 UI)를 점유 → 앱 멈춤.
    /// </para>
    /// <para>
    /// 정책: <see cref="NodeGraphViewModel"/> 의 <c>Run</c> 명령이 <c>_scheduler.RunAsync</c> 를
    /// <c>Task.Run</c> 으로 wrap → 모든 노드 실행이 ThreadPool 에서 진행. 호출 스레드는 첫 await 점에서
    /// 즉시 양보. 메시지 핸들러는 기존 UI 마샬링 인프라가 UI 로 다시 가져옴.
    /// </para>
    /// </summary>
    [Trait("Category", "Stress")]
    public class RunCommand_DoesNotBlockUiThreadTests
    {
        /// <summary>CPU 동기 sleep 으로 호출 스레드를 점유하는 테스트 노드.</summary>
        private sealed class CpuBoundNode : NodeBase
        {
            public const string TypeIdConst = "Test.CpuBound";
            public override string TypeId => TypeIdConst;

            public int? ExecutedOnThreadId { get; private set; }

            private static readonly PinDescriptor[] PinSpec = new[]
            {
                new PinDescriptor("In",   "In",   PinDirection.Input,  PinKind.Exec, typeof(void), null),
                new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void), null),
            };
            protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

            public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
            {
                ExecutedOnThreadId = Thread.CurrentThread.ManagedThreadId;
                // 100ms 동기 spin — 진짜 CPU 점유 시뮬레이션. Thread.Sleep 은 단순 대기지만 호출 스레드 점유 효과는 동일.
                Thread.Sleep(100);
                return Task.FromResult(ExecutionFlow.Continue("Then"));
            }

            public static NodeMetadata CreateMetadata(Func<CpuBoundNode> factory) => new NodeMetadata(
                TypeIdConst, "CpuBound", "Test", "Synchronous CPU work for thread test.", 0,
                typeof(CpuBoundNode), factory, PinSpec);
        }

        /// <summary>
        /// 호출 스레드 (테스트 — 가짜 UI) 에서 RunCommand.ExecuteAsync 를 호출했을 때,
        /// await 진입 후 노드는 ThreadPool 스레드에서 실행되어야 한다.
        /// </summary>
        [Fact(Timeout = 15_000)]
        public async Task Run_DoesNotExecuteCpuBoundNode_OnCallingThread()
        {
            // 1) 픽스처 등록 — CpuBoundNode 인스턴스를 우리가 보관해 ExecutedOnThreadId 검증.
            CpuBoundNode capturedNode = null;
            NodeMetadataRegistry.UnregisterForTests(CpuBoundNode.TypeIdConst);
            NodeMetadataRegistry.Register(CpuBoundNode.CreateMetadata(() =>
            {
                capturedNode = new CpuBoundNode();
                return capturedNode;
            }));

            try
            {
                BuiltInNodes.EnsureRegistered();
                var graph = new NodeGraph();
                var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
                var cpu = graph.AddNode(CpuBoundNode.TypeIdConst, 0, 0);
                var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);
                graph.Connect(start.Id, "Then", cpu.Id, "In");
                graph.Connect(cpu.Id, "Then", end.Id, "In");

                var scheduler = new SchedulerService();
                var messenger = new Messenger();
                var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: scheduler, messenger: messenger);
                vm.SelectedNode = vm.FindNode(start.Id);

                int callingThreadId = Thread.CurrentThread.ManagedThreadId;

                // 2) RunCommand 의 비동기 실행. ExecuteAsync 는 IAsyncRelayCommand 인터페이스 보장.
                await vm.RunCommand.ExecuteAsync(null);

                // 3) 노드는 ThreadPool 의 다른 스레드에서 실행되어야 한다.
                capturedNode.Should().NotBeNull();
                capturedNode.ExecutedOnThreadId.Should().NotBe(callingThreadId,
                    "CPU-바운드 노드가 호출 스레드(UI)를 점유하면 안 됨 — Task.Run 등으로 ThreadPool 로 offload 되어야 함");
            }
            finally
            {
                NodeMetadataRegistry.UnregisterForTests(CpuBoundNode.TypeIdConst);
            }
        }

        /// <summary>
        /// Run 호출 직후 await 점에서 호출 스레드로 제어가 즉시 돌아와야 한다 — 노드가 끝나기 전에.
        /// (Stopwatch 로 await 시작과 호출 사이 시간이 노드 작업 시간보다 훨씬 짧은지 확인.)
        /// </summary>
        [Fact(Timeout = 15_000)]
        public async Task Run_ReturnsControlToCaller_BeforeCpuBoundNodeFinishes()
        {
            NodeMetadataRegistry.UnregisterForTests(CpuBoundNode.TypeIdConst);
            NodeMetadataRegistry.Register(CpuBoundNode.CreateMetadata(() => new CpuBoundNode()));

            try
            {
                BuiltInNodes.EnsureRegistered();
                var graph = new NodeGraph();
                var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
                var cpu = graph.AddNode(CpuBoundNode.TypeIdConst, 0, 0);
                graph.Connect(start.Id, "Then", cpu.Id, "In");

                var scheduler = new SchedulerService();
                var messenger = new Messenger();
                var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: scheduler, messenger: messenger);
                vm.SelectedNode = vm.FindNode(start.Id);

                // Run 호출 후 await Task 가 즉시 (수 ms 안에) Task 객체 반환 — 노드의 100ms sleep 동안 await.
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var runTask = vm.RunCommand.ExecuteAsync(null);
                var callReturnedMs = sw.Elapsed.TotalMilliseconds;

                await runTask;
                var totalMs = sw.Elapsed.TotalMilliseconds;

                callReturnedMs.Should().BeLessThan(50,
                    $"ExecuteAsync 호출 자체는 즉시 Task 반환해야 (호출 후 {callReturnedMs:F1}ms). 그렇지 않으면 동기 노드가 호출 스레드 점유.");
                totalMs.Should().BeGreaterOrEqualTo(90,
                    "노드의 100ms 작업은 어쨌든 끝나야 await 가 완료");
            }
            finally
            {
                NodeMetadataRegistry.UnregisterForTests(CpuBoundNode.TypeIdConst);
            }
        }
    }
}
