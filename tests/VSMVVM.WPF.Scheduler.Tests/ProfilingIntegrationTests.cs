using System;
using System.Linq;
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
    /// Phase I.3 — NodeExitedMessage 가 NodeViewModel.LastElapsed 와 NodeGraphViewModel.Profiling 양쪽에 자동 반영되는지 검증.
    /// </summary>
    public class ProfilingIntegrationTests
    {
        private static (NodeGraphViewModel vm, NodeGraph graph, IMessenger msg) Build()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var msg = new Messenger();
            return (new NodeGraphViewModel(g, undoRedo: null, scheduler: new SchedulerService(), messenger: msg), g, msg);
        }

        [Fact]
        public void NodeExitedMessage_UpdatesNodeViewModelLastElapsed()
        {
            var (vm, graph, msg) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var nvm = vm.FindNode(start.Id);
            nvm.HasElapsedSample.Should().BeFalse();

            msg.Send(new NodeExitedMessage(
                runId: Guid.NewGuid(), graphId: graph.Id, nodeId: start.Id, nodeTypeId: StartNode.TypeIdConst,
                success: true, elapsed: TimeSpan.FromMilliseconds(42), error: null));

            nvm.LastElapsedMs.Should().Be(42);
            nvm.HasElapsedSample.Should().BeTrue();
        }

        [Fact]
        public void NodeExitedMessage_AccumulatesProfilingStats()
        {
            var (vm, graph, msg) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);

            msg.Send(new NodeExitedMessage(Guid.NewGuid(), graph.Id, start.Id, StartNode.TypeIdConst,
                true, TimeSpan.FromMilliseconds(10), null));
            msg.Send(new NodeExitedMessage(Guid.NewGuid(), graph.Id, start.Id, StartNode.TypeIdConst,
                true, TimeSpan.FromMilliseconds(30), null));

            var p = vm.Profiling.Get(start.Id).Value;
            p.Count.Should().Be(2);
            p.Min.Should().Be(TimeSpan.FromMilliseconds(10));
            p.Max.Should().Be(TimeSpan.FromMilliseconds(30));
            p.Last.Should().Be(TimeSpan.FromMilliseconds(30));
        }

        [Fact]
        public void IsSlow_TogglesAtSlowThreshold()
        {
            var (vm, graph, msg) = Build();
            var node = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var nvm = vm.FindNode(node.Id);
            nvm.SlowThresholdMs = 100;
            nvm.VerySlowThresholdMs = 1000;

            // 50ms — Slow 아님
            msg.Send(new NodeExitedMessage(Guid.NewGuid(), graph.Id, node.Id, StartNode.TypeIdConst,
                true, TimeSpan.FromMilliseconds(50), null));
            nvm.IsSlow.Should().BeFalse();
            nvm.IsVerySlow.Should().BeFalse();

            // 200ms — Slow
            msg.Send(new NodeExitedMessage(Guid.NewGuid(), graph.Id, node.Id, StartNode.TypeIdConst,
                true, TimeSpan.FromMilliseconds(200), null));
            nvm.IsSlow.Should().BeTrue();
            nvm.IsVerySlow.Should().BeFalse();

            // 1500ms — VerySlow (IsSlow는 false, IsVerySlow가 true — 우선순위 분리)
            msg.Send(new NodeExitedMessage(Guid.NewGuid(), graph.Id, node.Id, StartNode.TypeIdConst,
                true, TimeSpan.FromMilliseconds(1500), null));
            nvm.IsSlow.Should().BeFalse();
            nvm.IsVerySlow.Should().BeTrue();
        }

        // === Phase I.2a — Inspector 스냅샷 통합 ===

        [Fact]
        public void NodeExitedMessage_PopulatesLastInputsAndOutputs_OnViewModel()
        {
            var (vm, graph, msg) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var nvm = vm.FindNode(start.Id);
            nvm.HasSnapshot.Should().BeFalse();

            var inputs = new System.Collections.Generic.Dictionary<string, object> { { "X", 42 } };
            var outputs = new System.Collections.Generic.Dictionary<string, object> { { "Y", "hello" } };
            msg.Send(new NodeExitedMessage(
                runId: Guid.NewGuid(), graphId: graph.Id, nodeId: start.Id, nodeTypeId: StartNode.TypeIdConst,
                success: true, elapsed: TimeSpan.FromMilliseconds(1), error: null,
                inputs: inputs, outputs: outputs));

            nvm.HasSnapshot.Should().BeTrue();
            nvm.LastInputs.Should().HaveCount(1);
            nvm.LastInputs[0].PinId.Should().Be("X");
            nvm.LastInputs[0].Value.Should().Be(42);
            nvm.LastOutputs.Should().HaveCount(1);
            nvm.LastOutputs[0].PinId.Should().Be("Y");
            nvm.LastOutputs[0].Value.Should().Be("hello");
        }

        [Fact]
        public void NodeExitedMessage_SubsequentCalls_ReplaceSnapshot()
        {
            var (vm, graph, msg) = Build();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var nvm = vm.FindNode(start.Id);

            msg.Send(new NodeExitedMessage(Guid.NewGuid(), graph.Id, start.Id, StartNode.TypeIdConst,
                true, TimeSpan.FromMilliseconds(1), null,
                inputs: new System.Collections.Generic.Dictionary<string, object> { { "A", 1 } },
                outputs: null));
            nvm.LastInputs.Should().HaveCount(1);
            nvm.LastInputs[0].PinId.Should().Be("A");

            msg.Send(new NodeExitedMessage(Guid.NewGuid(), graph.Id, start.Id, StartNode.TypeIdConst,
                true, TimeSpan.FromMilliseconds(1), null,
                inputs: new System.Collections.Generic.Dictionary<string, object> { { "B", 2 }, { "C", 3 } },
                outputs: null));

            nvm.LastInputs.Should().HaveCount(2);
            nvm.LastInputs.Select(s => s.PinId).Should().BeEquivalentTo(new[] { "B", "C" });
        }
    }
}
