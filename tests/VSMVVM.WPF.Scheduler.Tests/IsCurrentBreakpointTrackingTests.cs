using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests
{
    /// <summary>
    /// 회귀 방지: 사용자가 BP 에 정지하거나 StepOver 로 다음 노드에서 정지했을 때,
    /// 어떤 노드가 "현재 정지 위치" 인지 시각적으로 알 수 있어야 한다.
    /// <para>
    /// 정책: <see cref="NodeViewModel.IsCurrentBreakpoint"/> 가 paused 노드 1개만 true 로 유지.
    /// BreakpointHitMessage 도착 시 해당 노드만 set + 다른 모든 노드는 clear.
    /// NodeEnteringMessage (정지 풀려 그 노드가 실제 실행 진입) 시 clear.
    /// GraphCompletedMessage 시 전 노드 clear.
    /// </para>
    /// </summary>
    public class IsCurrentBreakpointTrackingTests
    {
        // 모든 메시지 핸들러를 UI 마샬링 없이 직접 호출되도록 SyncContext null.
        public IsCurrentBreakpointTrackingTests() => SynchronizationContext.SetSynchronizationContext(null);

        [Fact]
        public void BreakpointHit_MarksTargetNode_AsCurrentBreakpoint()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var messenger = new Messenger();
            var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: null, messenger: messenger);

            var logVm = vm.FindNode(log.Id);
            logVm.IsCurrentBreakpoint.Should().BeFalse("초기 상태");

            messenger.Send(new BreakpointHitMessage(Guid.NewGuid(), graph.Id, log.Id));

            logVm.IsCurrentBreakpoint.Should().BeTrue("BP 도달한 노드는 IsCurrentBreakpoint=true");
            vm.FindNode(start.Id).IsCurrentBreakpoint.Should().BeFalse("다른 노드는 false 유지");
        }

        [Fact]
        public void BreakpointHit_ClearsPreviouslyPausedNode()
        {
            // StepOver 시나리오 — 첫 BP 에서 정지 후 StepOver → 다음 노드에서 다시 BP.
            // 이전 노드의 IsCurrentBreakpoint 는 자동으로 false 가 되어야 (한 번에 한 노드만 paused).
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var n1 = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var n2 = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var messenger = new Messenger();
            var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: null, messenger: messenger);

            messenger.Send(new BreakpointHitMessage(Guid.NewGuid(), graph.Id, n1.Id));
            vm.FindNode(n1.Id).IsCurrentBreakpoint.Should().BeTrue();

            messenger.Send(new BreakpointHitMessage(Guid.NewGuid(), graph.Id, n2.Id));

            vm.FindNode(n1.Id).IsCurrentBreakpoint.Should().BeFalse("새 BP 도달 시 이전 BP 노드는 clear");
            vm.FindNode(n2.Id).IsCurrentBreakpoint.Should().BeTrue();
        }

        [Fact]
        public void NodeEntering_ClearsCurrentBreakpoint_OnThatNode()
        {
            // Continue 후 정지된 노드가 실제 실행 진입 시 — IsExecuting=true 가 되고 IsCurrentBreakpoint=false 로 전환.
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var node = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var messenger = new Messenger();
            var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: null, messenger: messenger);

            messenger.Send(new BreakpointHitMessage(Guid.NewGuid(), graph.Id, node.Id));
            vm.FindNode(node.Id).IsCurrentBreakpoint.Should().BeTrue();

            messenger.Send(new NodeEnteringMessage(Guid.NewGuid(), graph.Id, node.Id, node.TypeId));

            var nvm = vm.FindNode(node.Id);
            nvm.IsCurrentBreakpoint.Should().BeFalse("실행 진입 시 paused 상태 해제");
            nvm.IsExecuting.Should().BeTrue("실행 진입 표시");
        }

        [Fact]
        public void GraphCompleted_ClearsAllCurrentBreakpoints()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var node = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var messenger = new Messenger();
            var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: null, messenger: messenger);

            messenger.Send(new BreakpointHitMessage(Guid.NewGuid(), graph.Id, node.Id));
            vm.FindNode(node.Id).IsCurrentBreakpoint.Should().BeTrue();

            var result = new ExecutionResult(Guid.NewGuid(), ExecutionStatus.Completed, 1, null, TimeSpan.Zero,
                new Dictionary<string, object>());
            messenger.Send(new GraphCompletedMessage(graph.Id, result));

            vm.FindNode(node.Id).IsCurrentBreakpoint.Should().BeFalse("그래프 완료 시 모든 paused 표시 해제");
        }

        [Fact]
        public void BreakpointHit_UnknownNodeId_DoesNotThrow_AndClearsPrevious()
        {
            // 일치하는 NodeViewModel 이 없어도 silent — 다른 워크스페이스의 메시지가 잘못 도달한 시나리오 등.
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var node = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var messenger = new Messenger();
            var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: null, messenger: messenger);

            messenger.Send(new BreakpointHitMessage(Guid.NewGuid(), graph.Id, node.Id));
            vm.FindNode(node.Id).IsCurrentBreakpoint.Should().BeTrue();

            var act = () => messenger.Send(new BreakpointHitMessage(Guid.NewGuid(), graph.Id, Guid.NewGuid()));
            act.Should().NotThrow();

            // 알 수 없는 NodeId 라도 "현재 paused 위치 갱신" 의미로 기존 노드는 clear.
            vm.FindNode(node.Id).IsCurrentBreakpoint.Should().BeFalse();
        }
    }
}
