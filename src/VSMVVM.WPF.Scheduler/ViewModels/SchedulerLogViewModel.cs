using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// I.4 — ISchedulerLogSink 의 EntryWritten 이벤트를 구독해 ObservableCollection 으로 노출.
    /// 패널은 이 VM 의 Entries 를 ItemsControl 로 바인딩. UI 스레드 marshal 은 Dispatcher.
    /// </summary>
    public partial class SchedulerLogViewModel : ViewModelBase
    {
        private readonly ISchedulerLogSink _sink;
        private readonly Dispatcher _dispatcher;
        private bool _disposed;

        public ObservableCollection<SchedulerLogEntry> Entries { get; } = new();

        /// <summary>최대 표시 항목 수. 초과 시 가장 오래된 항목 제거.</summary>
        [Property] private int _maxDisplay = 500;

        public SchedulerLogViewModel(ISchedulerLogSink sink, Dispatcher dispatcher = null)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
            foreach (var e in _sink.GetAll())
            {
                Entries.Add(e);
            }
            _sink.EntryWritten += OnEntry;
        }

        private void OnEntry(object sender, SchedulerLogEntry entry)
        {
            if (_disposed) return;
            if (_dispatcher.CheckAccess())
            {
                Append(entry);
            }
            else
            {
                _dispatcher.BeginInvoke(new Action(() => Append(entry)));
            }
        }

        private void Append(SchedulerLogEntry e)
        {
            Entries.Add(e);
            while (Entries.Count > MaxDisplay)
            {
                Entries.RemoveAt(0);
            }
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
