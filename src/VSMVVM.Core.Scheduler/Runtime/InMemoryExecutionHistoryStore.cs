using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>
    /// 메모리 LRU 보관소 — 가장 오래된 run 부터 제거. 기본 capacity 100.
    /// 스레드 안전: 모든 변이 작업에 lock(_sync).
    /// </summary>
    public sealed class InMemoryExecutionHistoryStore : IExecutionHistoryStore
    {
        private readonly object _sync = new object();
        private readonly LinkedList<ExecutionRun> _runs = new LinkedList<ExecutionRun>();

        public int Capacity { get; }

        public InMemoryExecutionHistoryStore(int capacity = 100)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public event EventHandler<ExecutionRun> RunAdded;

        public void Add(ExecutionRun run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            lock (_sync)
            {
                _runs.AddLast(run);
                while (_runs.Count > Capacity)
                {
                    _runs.RemoveFirst();
                }
            }
            RunAdded?.Invoke(this, run);
        }

        public IReadOnlyList<ExecutionRun> GetAll()
        {
            lock (_sync)
            {
                return new List<ExecutionRun>(_runs);
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _runs.Clear();
            }
        }
    }
}
