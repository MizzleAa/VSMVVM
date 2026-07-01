using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>SchedulerService.RunAsync 의 반환값.</summary>
    public sealed class ExecutionResult
    {
        private static readonly IReadOnlyDictionary<string, object> EmptyOutputs
            = new Dictionary<string, object>(0);

        public Guid RunId { get; }
        public ExecutionStatus Status { get; }
        public int NodesExecuted { get; }
        public Exception Error { get; }
        public TimeSpan Elapsed { get; }

        /// <summary>I.2c — OutputNode 가 채운 결과 키-값 (ExecutionContext.Outputs 스냅샷).</summary>
        public IReadOnlyDictionary<string, object> Outputs { get; }

        public ExecutionResult(Guid runId, ExecutionStatus status, int nodesExecuted,
                               Exception error, TimeSpan elapsed,
                               IReadOnlyDictionary<string, object> outputs = null)
        {
            RunId = runId;
            Status = status;
            NodesExecuted = nodesExecuted;
            Error = error;
            Elapsed = elapsed;
            Outputs = outputs ?? EmptyOutputs;
        }
    }
}
