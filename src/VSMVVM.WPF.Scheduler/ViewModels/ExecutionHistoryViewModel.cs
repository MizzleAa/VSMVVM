using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// I.2b — IExecutionHistoryStore 의 RunAdded 이벤트를 ObservableCollection 으로 노출.
    /// 선택된 ExecutionRun 의 Records 를 간트 차트로 표시하는 SelectedRun 프로퍼티.
    /// 옵션으로 NodeGraphViewModel 을 주입하면 BreakpointNodeIds 를 노출 — 간트차트가 강조용으로 사용.
    /// </summary>
    public partial class ExecutionHistoryViewModel : ViewModelBase
    {
        private readonly IExecutionHistoryStore _store;
        private readonly Dispatcher _dispatcher;
        private bool _disposed;
        private NodeGraphViewModel _graphVm;

        public ObservableCollection<ExecutionRun> Runs { get; } = new();

        /// <summary>NodeGraphViewModel 의 노드들 중 HasBreakpoint=true 인 NodeId 집합 (라이브).</summary>
        public ObservableCollection<Guid> BreakpointNodeIds { get; } = new();

        [Property] private ExecutionRun _selectedRun;

        /// <summary>옵션 — 간트차트가 브레이크포인트 행을 강조하도록 NodeGraphViewModel 을 연결한다.</summary>
        public NodeGraphViewModel Graph
        {
            get => _graphVm;
            set
            {
                if (_graphVm == value) return;
                Unsubscribe();
                _graphVm = value;
                Subscribe();
                RefreshBreakpoints();
            }
        }

        private void Subscribe()
        {
            if (_graphVm == null) return;
            foreach (var n in _graphVm.Nodes) n.PropertyChanged += OnNodePropertyChanged;
            ((System.Collections.Specialized.INotifyCollectionChanged)_graphVm.Nodes).CollectionChanged += OnNodesCollectionChanged;
        }

        private void Unsubscribe()
        {
            if (_graphVm == null) return;
            foreach (var n in _graphVm.Nodes) n.PropertyChanged -= OnNodePropertyChanged;
            ((System.Collections.Specialized.INotifyCollectionChanged)_graphVm.Nodes).CollectionChanged -= OnNodesCollectionChanged;
        }

        private void OnNodesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (NodeViewModel n in e.NewItems) n.PropertyChanged += OnNodePropertyChanged;
            if (e.OldItems != null) foreach (NodeViewModel n in e.OldItems) n.PropertyChanged -= OnNodePropertyChanged;
            RefreshBreakpoints();
        }

        private void OnNodePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NodeViewModel.HasBreakpoint)) RefreshBreakpoints();
        }

        private void RefreshBreakpoints()
        {
            BreakpointNodeIds.Clear();
            if (_graphVm == null) return;
            foreach (var n in _graphVm.Nodes)
            {
                if (n.HasBreakpoint) BreakpointNodeIds.Add(n.Id);
            }
        }

        public ExecutionHistoryViewModel(IExecutionHistoryStore store, Dispatcher dispatcher = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
            foreach (var r in _store.GetAll())
            {
                Runs.Add(r);
            }
            _store.RunAdded += OnRunAdded;
        }

        private void OnRunAdded(object sender, ExecutionRun run)
        {
            if (_disposed) return;
            if (_dispatcher.CheckAccess())
            {
                Runs.Add(run);
                SelectedRun = run; // 가장 최근 run 자동 선택
            }
            else
            {
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    Runs.Add(run);
                    SelectedRun = run;
                }));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;
            if (disposing)
            {
                _store.RunAdded -= OnRunAdded;
            }
            base.Dispose(disposing);
        }
    }
}
