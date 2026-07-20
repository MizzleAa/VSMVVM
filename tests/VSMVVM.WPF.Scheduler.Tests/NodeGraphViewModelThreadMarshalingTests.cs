using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    /// 회귀 방지: SchedulerService 는 노드를 백그라운드 스레드에서 실행하므로 NodeExitedMessage 같은
    /// 런타임 메시지가 UI 가 아닌 스레드에서 발화될 수 있다. NodeGraphViewModel 의 핸들러가 그 메시지를
    /// 받아 ObservableCollection (LastInputs/LastOutputs) 를 직접 수정하면 WPF CollectionView 가
    /// "다른 스레드의 변경" 으로 throw.
    /// <para>
    /// 정책: NodeGraphViewModel 이 생성 시점의 <see cref="SynchronizationContext"/> 를 capture 하고,
    /// 모든 메시지 핸들러가 capture 한 context 로 마샬링한다. capture 가 null (테스트/콘솔) 이면 직접 호출.
    /// </para>
    /// </summary>
    [Trait("Category", "Stress")]
    public class NodeGraphViewModelThreadMarshalingTests
    {
        /// <summary>큐잉 SyncContext — Post 된 콜백이 Drain 호출 시 실행. capture 가능한 SynchronizationContext.</summary>
        private sealed class QueuedSyncContext : SynchronizationContext
        {
            public List<(SendOrPostCallback cb, object state)> Posted { get; } = new();
            public override void Post(SendOrPostCallback d, object state) => Posted.Add((d, state));
            public override void Send(SendOrPostCallback d, object state) => d(state);
            public void Drain() { foreach (var (cb, state) in Posted) cb(state); Posted.Clear(); }
        }

        [Fact]
        public void NodeExitedMessage_FromOtherThread_MarshalsToCapturedContext()
        {
            // 1) UI 스레드 시뮬레이션 — VM 을 capture 가능한 context 위에서 생성.
            var ui = new QueuedSyncContext();
            SynchronizationContext.SetSynchronizationContext(ui);

            try
            {
                BuiltInNodes.EnsureRegistered();
                var graph = new NodeGraph();
                var node = graph.AddNode(StartNode.TypeIdConst, 0, 0);
                var messenger = new Messenger();
                var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: null, messenger: messenger);
                var nvm = vm.FindNode(node.Id);

                int? handlerThread = null;
                // HasExecutedInCurrentRun 트랜지션 false→true 로 핸들러 실행 스레드 식별 — IsExecuting 은
                // 초기값/대상값 모두 false 라 setter 가드로 PropertyChanged 발화 안 함.
                nvm.PropertyChanged += (_, ev) =>
                {
                    if (ev.PropertyName == nameof(NodeViewModel.HasExecutedInCurrentRun))
                        handlerThread = Thread.CurrentThread.ManagedThreadId;
                };

                int uiThread = Thread.CurrentThread.ManagedThreadId;

                int? bgThread = null;
                var t = new Thread(() =>
                {
                    bgThread = Thread.CurrentThread.ManagedThreadId;
                    messenger.Send(new NodeExitedMessage(
                        Guid.NewGuid(), graph.Id, node.Id, node.TypeId,
                        success: true, TimeSpan.FromMilliseconds(1), error: null,
                        inputs: new Dictionary<string, object>(),
                        outputs: new Dictionary<string, object>()));
                });
                t.Start();
                t.Join();

                bgThread.Should().NotBe(uiThread, "테스트 전제 — 백그라운드가 UI 와 달라야 함");
                handlerThread.Should().BeNull("핸들러는 capture context 에 Post 되어 Drain 전까지 실행 안 됨");
                ui.Posted.Should().NotBeEmpty("백그라운드 메시지가 UI capture 에 Post 되어야 함");

                ui.Drain();
                handlerThread.Should().Be(uiThread, "Drain 후 핸들러는 capture 한 context(UI) 스레드에서 실행");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Fact]
        public void NodeEnteringMessage_FromOtherThread_MarshalsToCapturedContext()
        {
            var ui = new QueuedSyncContext();
            SynchronizationContext.SetSynchronizationContext(ui);

            try
            {
                BuiltInNodes.EnsureRegistered();
                var graph = new NodeGraph();
                var node = graph.AddNode(StartNode.TypeIdConst, 0, 0);
                var messenger = new Messenger();
                var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: null, messenger: messenger);

                var t = new Thread(() =>
                {
                    messenger.Send(new NodeEnteringMessage(Guid.NewGuid(), graph.Id, node.Id, node.TypeId));
                });
                t.Start();
                t.Join();

                ui.Posted.Should().NotBeEmpty();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Fact]
        public void GraphCompletedMessage_FromOtherThread_MarshalsToCapturedContext()
        {
            var ui = new QueuedSyncContext();
            SynchronizationContext.SetSynchronizationContext(ui);

            try
            {
                BuiltInNodes.EnsureRegistered();
                var graph = new NodeGraph();
                var messenger = new Messenger();
                var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: null, messenger: messenger);

                var t = new Thread(() =>
                {
                    var result = new ExecutionResult(Guid.NewGuid(), ExecutionStatus.Completed, 0, null, TimeSpan.Zero,
                        new Dictionary<string, object>());
                    messenger.Send(new GraphCompletedMessage(graph.Id, result));
                });
                t.Start();
                t.Join();

                ui.Posted.Should().NotBeEmpty();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Fact]
        public void BreakpointHitMessage_FromOtherThread_MarshalsToCapturedContext()
        {
            var ui = new QueuedSyncContext();
            SynchronizationContext.SetSynchronizationContext(ui);

            try
            {
                BuiltInNodes.EnsureRegistered();
                var graph = new NodeGraph();
                var node = graph.AddNode(StartNode.TypeIdConst, 0, 0);
                var messenger = new Messenger();
                var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: null, messenger: messenger);

                var t = new Thread(() =>
                {
                    messenger.Send(new BreakpointHitMessage(Guid.NewGuid(), graph.Id, node.Id));
                });
                t.Start();
                t.Join();

                ui.Posted.Should().NotBeEmpty();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Fact]
        public void NoSyncContext_FallsBackToDirectInvoke()
        {
            // SynchronizationContext.Current 가 null (테스트/콘솔/non-UI) 에서는 마샬링 없이 직접 호출.
            SynchronizationContext.SetSynchronizationContext(null);

            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var node = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var messenger = new Messenger();
            var vm = new NodeGraphViewModel(graph, undoRedo: null, scheduler: null, messenger: messenger);
            var nvm = vm.FindNode(node.Id);

            int? handlerThread = null;
            int? bgThread = null;
            nvm.PropertyChanged += (_, ev) =>
            {
                if (ev.PropertyName == nameof(NodeViewModel.IsExecuting))
                    handlerThread = Thread.CurrentThread.ManagedThreadId;
            };

            var t = new Thread(() =>
            {
                bgThread = Thread.CurrentThread.ManagedThreadId;
                messenger.Send(new NodeEnteringMessage(Guid.NewGuid(), graph.Id, node.Id, node.TypeId));
            });
            t.Start();
            t.Join();

            handlerThread.Should().Be(bgThread, "capture context 가 null 이면 호출 스레드에서 직접 실행");
        }
    }
}
