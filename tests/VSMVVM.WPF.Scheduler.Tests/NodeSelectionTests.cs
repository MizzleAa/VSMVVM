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
    /// 노드 선택 상태 — SelectedNode 변경 시 이전 노드의 IsSelected 가 자동 false 처리되어야 함.
    /// 누적 선택(여러 노드 동시 강조)이 되지 않도록.
    /// </summary>
    public class NodeSelectionTests
    {
        private static (NodeGraphViewModel vm, NodeViewModel a, NodeViewModel b) Build()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var na = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var nb = g.AddNode(EndNode.TypeIdConst, 0, 0);
            var vm = new NodeGraphViewModel(g, undoRedo: null, scheduler: new SchedulerService(), messenger: new Messenger());
            return (vm, vm.FindNode(na.Id), vm.FindNode(nb.Id));
        }

        [Fact]
        public void SelectingNode_SetsItsIsSelectedTrue()
        {
            var (vm, a, _) = Build();
            vm.SelectedNode = a;
            a.IsSelected.Should().BeTrue();
        }

        [Fact]
        public void SelectingDifferentNode_ClearsPreviousIsSelected()
        {
            var (vm, a, b) = Build();
            vm.SelectedNode = a;
            a.IsSelected.Should().BeTrue();

            vm.SelectedNode = b;

            a.IsSelected.Should().BeFalse("이전 노드는 자동으로 선택 해제되어야 함");
            b.IsSelected.Should().BeTrue();
        }

        [Fact]
        public void SelectingNull_ClearsAll()
        {
            var (vm, a, _) = Build();
            vm.SelectedNode = a;
            a.IsSelected.Should().BeTrue();

            vm.SelectedNode = null;
            a.IsSelected.Should().BeFalse();
        }
    }
}
