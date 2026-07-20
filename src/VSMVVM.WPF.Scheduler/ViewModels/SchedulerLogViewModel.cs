using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Threading;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// I.4 — ISchedulerLogSink 의 EntryWritten 이벤트를 구독해 ObservableCollection 으로 노출.
    /// 패널은 이 VM 의 Entries 를 ItemsControl 로 바인딩.
    ///
    /// 부하 대응: EntryWritten 이 폭주해도 UI 스레드가 굳지 않도록 coalescing 배치 처리.
    /// - background 이벤트는 ConcurrentQueue 에 push 만.
    /// - 첫 이벤트가 들어올 때만 Dispatcher.BeginInvoke 예약 (Interlocked flag 로 중복 예약 차단).
    /// - flush 콜백에서 큐 전체 drain → Entries 에 batch add → 한 번만 trim.
    ///   결과: CollectionChanged 발생 수가 write 수에서 배치 수로 급감 (WPF ItemsControl 재레이아웃 폭발 방지).
    /// </summary>
    public partial class SchedulerLogViewModel : ViewModelBase
    {
        private readonly ISchedulerLogSink _sink;
        private readonly Dispatcher _dispatcher;
        private readonly ConcurrentQueue<SchedulerLogEntry> _pending = new ConcurrentQueue<SchedulerLogEntry>();
        private readonly BulkObservableCollection<SchedulerLogEntry> _entries = new();
        private int _flushScheduled; // 0 = 예약 없음, 1 = 예약됨.
        private bool _disposed;

        public ObservableCollection<SchedulerLogEntry> Entries => _entries;

        /// <summary>최대 표시 항목 수. 초과 시 가장 오래된 항목 제거.</summary>
        [Property] private int _maxDisplay = 100;

        public SchedulerLogViewModel(ISchedulerLogSink sink, Dispatcher dispatcher = null)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
            var initial = _sink.GetAll();
            if (initial.Count > 0)
            {
                var list = new List<SchedulerLogEntry>(initial.Count);
                for (int i = 0; i < initial.Count; i++) list.Add(initial[i]);
                _entries.AddRange(list);
            }
            TrimExcess();
            _sink.EntryWritten += OnEntry;
        }

        private void OnEntry(object sender, SchedulerLogEntry entry)
        {
            if (_disposed) return;
            _pending.Enqueue(entry);
            // UI 스레드에서 직접 호출됐고 flush 예약이 없으면 즉시 flush — 단일 이벤트 latency 최소화.
            if (_dispatcher.CheckAccess() && Volatile.Read(ref _flushScheduled) == 0)
            {
                Flush();
                return;
            }
            // 이미 flush 예약이 걸려 있으면 추가 예약 안 함 → BeginInvoke 큐 폭발 방지.
            if (Interlocked.CompareExchange(ref _flushScheduled, 1, 0) == 0)
            {
                _dispatcher.BeginInvoke(new Action(Flush), DispatcherPriority.Background);
            }
        }

        private void Flush()
        {
            Volatile.Write(ref _flushScheduled, 0);
            if (_disposed) return;

            var batch = new List<SchedulerLogEntry>();
            while (_pending.TryDequeue(out var e)) batch.Add(e);
            if (batch.Count == 0) return;

            _entries.AddRange(batch);
            TrimExcess();
        }

        private void TrimExcess()
        {
            int overflow = _entries.Count - MaxDisplay;
            if (overflow > 0) _entries.RemoveRangeFromStart(overflow);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;
            if (disposing)
            {
                _sink.EntryWritten -= OnEntry;
            }
            base.Dispose(disposing);
        }
    }
}
