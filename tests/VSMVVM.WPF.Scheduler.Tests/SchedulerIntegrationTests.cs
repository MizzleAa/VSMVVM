using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.WPF.Scheduler.Tests
{
    public class SchedulerIntegrationTests
    {
        private static (NodeGraphViewModel vm, NodeGraph graph, IMessenger msg, SchedulerService svc)
            Build()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var msg = new Messenger();
            var svc = new SchedulerService();
            var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: svc, messenger: msg);
            return (vm, graph, msg, svc);
        }

        [Fact]
        public void NodeEnteringMessage_SetsIsExecutingTrue_OnMatchingNodeViewModel()
        {
            var (vm, graph, msg, _) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);

            msg.Send(new NodeEnteringMessage(Guid.NewGuid(), graph.Id, start.Id, StartNode.TypeIdConst));

            var nvm = vm.FindNode(start.Id);
            nvm.IsExecuting.Should().BeTrue();
        }

        [Fact]
        public void NodeExitedMessage_SetsIsExecutingFalse()
        {
            var (vm, graph, msg, _) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var nvm = vm.FindNode(start.Id);
            nvm.IsExecuting = true;

            msg.Send(new NodeExitedMessage(Guid.NewGuid(), graph.Id, start.Id, StartNode.TypeIdConst,
                success: true, elapsed: TimeSpan.Zero, error: null));

            nvm.IsExecuting.Should().BeFalse();
        }

        [Fact]
        public void BreakpointHitMessage_SetsIsPausedTrue()
        {
            var (vm, graph, msg, _) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);

            msg.Send(new BreakpointHitMessage(Guid.NewGuid(), graph.Id, start.Id));

            vm.IsPaused.Should().BeTrue();
        }

        [Fact]
        public void GraphCompletedMessage_ResetsIsRunning_AndClearsIsExecuting()
        {
            var (vm, graph, msg, _) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var nvm = vm.FindNode(start.Id);
            nvm.IsExecuting = true;
            vm.IsRunning = true;
            vm.IsPaused = true;

            var result = new ExecutionResult(Guid.NewGuid(), ExecutionStatus.Completed, 1, null, TimeSpan.Zero);
            msg.Send(new GraphCompletedMessage(graph.Id, result));

            vm.IsRunning.Should().BeFalse();
            vm.IsPaused.Should().BeFalse();
            nvm.IsExecuting.Should().BeFalse();
        }

        [Fact]
        public void ToggleBreakpointCommand_TogglesHasBreakpointOnSelectedNode()
        {
            var (vm, graph, _, _) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            vm.SelectedNode = vm.FindNode(start.Id);

            vm.ToggleBreakpointCommand.Execute(null);
            vm.SelectedNode.HasBreakpoint.Should().BeTrue();

            vm.ToggleBreakpointCommand.Execute(null);
            vm.SelectedNode.HasBreakpoint.Should().BeFalse();
        }

        [Fact]
        public async Task Run_ExecutesGraph_AndTransitionsIsRunning()
        {
            var (vm, graph, _, _) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            graph.Connect(start.Id, "Then", end.Id, "In");
            vm.SelectedNode = vm.FindNode(start.Id);

            await vm.RunCommand.ExecuteAsync(null);

            vm.IsRunning.Should().BeFalse();   // GraphCompleted 후 false 복원
            vm.IsPaused.Should().BeFalse();
        }

        [Fact]
        [Trait("Category", "Stress")]
        public async Task Breakpoint_PausesRun_ContinueResumes()
        {
            // Start → BranchNode (브레이크포인트) → True/False End. 사실 SchedulerService의 게이트는
            // _continueGate를 사용 — 한 스레드에서 Continue 가 Run 보다 먼저 호출되면 의도와 다르게 통과.
            // 검증: Run 시작 직후 잠시 후 Continue 가 호출되면 정상 진행.
            var (vm, graph, msg, svc) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);
            graph.Connect(start.Id, "Then", end.Id, "In");
            vm.FindNode(end.Id).HasBreakpoint = true;
            vm.SelectedNode = vm.FindNode(start.Id);

            // BreakpointHitMessage를 받자마자 Continue
            var paused = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            msg.Register<BreakpointHitMessage>(this, (s, m) => paused.TrySetResult(true));

            var runTask = vm.RunCommand.ExecuteAsync(null);

            await paused.Task; // 일시정지 신호 대기
            vm.IsPaused.Should().BeTrue();
            vm.ContinueCommand.Execute(null);

            await runTask;
            vm.IsRunning.Should().BeFalse();
        }
    }
}
