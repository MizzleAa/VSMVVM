using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>
    /// 한 그래프 실행(Run) 의 누적 기록 — SchedulerService 가 RunAsync 시작 시 생성, 노드 종료마다 NodeExecutionRecord 추가, 완료 시 IExecutionHistoryStore 에 push.
    /// 변이 가능(Records 는 List). 외부에는 IReadOnlyList 로 노출.
    /// </summary>
    public sealed class ExecutionRun
    {
        private readonly List<NodeExecutionRecord> _records = new List<NodeExecutionRecord>();

        public Guid Id { get; }
        public Guid GraphId { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset? CompletedAt { get; private set; }
        public ExecutionStatus Status { get; private set; } = ExecutionStatus.Running;
        public Exception Error { get; private set; }

        public IReadOnlyList<NodeExecutionRecord> Records => _records;

        /// <summary>한 run 전체 소요. 완료 후에만 유효 (그 전엔 TimeSpan.Zero).</summary>
        public TimeSpan Elapsed => CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt
            : TimeSpan.Zero;

        public ExecutionRun(Guid id, Guid graphId, DateTimeOffset startedAt)
        {
            Id = id;
            GraphId = graphId;
            StartedAt = startedAt;
        }

        /// <summary>
        /// 실행 중 노드 종료마다 SchedulerService 가 호출. 외부에도 열어두어 replay/import/데모 시나리오
        /// (예: 부하 테스트, 저장된 run 복원) 에서 ExecutionRun 을 재구성할 수 있게 함.
        /// </summary>
        public void AppendRecord(NodeExecutionRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            _records.Add(record);
        }

        /// <summary>
        /// run 을 종료 상태로 마감. SchedulerService 가 마지막에 호출.
        /// replay/import 시나리오를 위해 외부 노출.
        /// </summary>
        public void MarkCompleted(ExecutionStatus status, DateTimeOffset completedAt, Exception error = null)
        {
            Status = status;
            CompletedAt = completedAt;
            Error = error;
        }

        internal void AddRecord(NodeExecutionRecord record) => AppendRecord(record);
        internal void Complete(ExecutionStatus status, DateTimeOffset completedAt, Exception error)
            => MarkCompleted(status, completedAt, error);
    }
}
