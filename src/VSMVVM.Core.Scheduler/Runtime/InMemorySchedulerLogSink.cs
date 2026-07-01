using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>
    /// 메모리 ring 로그 — capacity 초과 시 가장 오래된 항목 제거. 기본 1000.
    /// 스레드 안전 (lock(_sync)).
    /// </summary>
    public sealed class InMemorySchedulerLogSink : ISchedulerLogSink
    {
        private readonly object _sync = new object();
        private readonly LinkedList<SchedulerLogEntry> _entries = new LinkedList<SchedulerLogEntry>();

        public int Capacity { get; }

        public InMemorySchedulerLogSink(int capacity = 1000)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public event EventHandler<SchedulerLogEntry> EntryWritten;

        public void Write(SchedulerLogEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            lock (_sync)
            {
                _entries.AddLast(entry);
                while (_entries.Count > Capacity)
                {
                    _entries.RemoveFirst();
                }
            }
            EntryWritten?.Invoke(this, entry);
        }

        public IReadOnlyList<SchedulerLogEntry> GetAll()
        {
            lock (_sync)
            {
                return new List<SchedulerLogEntry>(_entries);
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _entries.Clear();
            }
        }
    }
}
