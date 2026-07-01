using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>
    /// SchedulerLogEntry 의 소비자. 메모리/파일/원격 sink 등 다양하게 구현 가능.
    /// Scheduler 가 ExecutionContext.LogSink 로 호출하며, null 이면 logging skip.
    /// </summary>
    public interface ISchedulerLogSink
    {
        void Write(SchedulerLogEntry entry);
        IReadOnlyList<SchedulerLogEntry> GetAll();
        void Clear();
        event EventHandler<SchedulerLogEntry> EntryWritten;
    }
}
