using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
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
        private readonly ConcurrentQueue<ExecutionRun> _pending = new ConcurrentQueue<ExecutionRun>();
        private readonly BulkObservableCollection<ExecutionRun> _runs = new();
        private int _flushScheduled;
        private bool _disposed;
        private NodeGraphViewModel _graphVm;

        public ObservableCollection<ExecutionRun> Runs => _runs;

        /// <summary>NodeGraphViewModel 의 노드들 중 HasBreakpoint=true 인 NodeId 집합 (라이브).</summary>
        public ObservableCollection<Guid> BreakpointNodeIds { get; } = new();

        [Property] private ExecutionRun _selectedRun;

        /// <summary>최대 유지 Runs 개수. 초과 시 가장 오래된 항목 제거. 0 이하면 무제한.</summary>
        [Property] private int _maxRuns = 10;

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
            var initial = _store.GetAll();
            if (initial.Count > 0)
            {
                var list = new List<ExecutionRun>(initial.Count);
                for (int i = 0; i < initial.Count; i++) list.Add(initial[i]);
                _runs.AddRange(list);
            }
            TrimExcess();
            _store.RunAdded += OnRunAdded;
        }

        /// <summary>
        /// 부하 대응: RunAdded 폭주 시 UI 스레드가 굳지 않도록 coalescing 배치 처리.
        /// 큐에 push 후 flush 예약 1회. flush 콜백이 큐 전체를 한 번에 처리 → CollectionChanged 폭발 방지.
        /// </summary>
        private void OnRunAdded(object sender, ExecutionRun run)
        {
            if (_disposed) return;
            _pending.Enqueue(run);
            if (_dispatcher.CheckAccess() && Volatile.Read(ref _flushScheduled) == 0)
            {
                Flush();
                return;
            }
            if (Interlocked.CompareExchange(ref _flushScheduled, 1, 0) == 0)
            {
                _dispatcher.BeginInvoke(new Action(Flush), DispatcherPriority.Background);
            }
        }

        private void Flush()
        {
            Volatile.Write(ref _flushScheduled, 0);
            if (_disposed) return;

            var batch = new List<ExecutionRun>();
            while (_pending.TryDequeue(out var r)) batch.Add(r);
            if (batch.Count == 0) return;

            _runs.AddRange(batch);
            TrimExcess();
            SelectedRun = batch[batch.Count - 1]; // 배치 마지막 run 만 선택 → SelectedRun change 폭발 방지.
        }

        private void TrimExcess()
        {
            if (MaxRuns <= 0) return;
            int overflow = _runs.Count - MaxRuns;
            if (overflow > 0) _runs.RemoveRangeFromStart(overflow);
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
