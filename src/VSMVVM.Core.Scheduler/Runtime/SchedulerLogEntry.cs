using System;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>I.4 — Scheduler 가 발화하는 구조화된 로그 항목 1개.</summary>
    public sealed class SchedulerLogEntry
    {
        public DateTimeOffset Timestamp { get; }
        public SchedulerLogLevel Level { get; }
        public Guid RunId { get; }
        public Guid? NodeId { get; }
        public string NodeTypeId { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public SchedulerLogEntry(DateTimeOffset timestamp, SchedulerLogLevel level,
                                 Guid runId, Guid? nodeId, string nodeTypeId,
                                 string message, Exception exception)
        {
            Timestamp = timestamp;
            Level = level;
            RunId = runId;
            NodeId = nodeId;
            NodeTypeId = nodeTypeId;
            Message = message ?? string.Empty;
            Exception = exception;
        }
    }

    public enum SchedulerLogLevel
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
    }
}
