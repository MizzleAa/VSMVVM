using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>
    /// 한 노드의 한 번 실행 기록 — ExecutionRun.Records 에 누적된다.
    /// 시간/입출력 스냅샷/오류를 모두 보관하므로 인스펙터/간트 차트/로그 모두의 데이터 소스.
    /// 불변(record).
    /// </summary>
    public sealed class NodeExecutionRecord
    {
        public Guid NodeId { get; }
        public string TypeId { get; }
        public DateTimeOffset StartedAt { get; }
        public TimeSpan Elapsed { get; }

        /// <summary>노드 실행 시점의 데이터 입력 핀 스냅샷. 비어있을 수 있음.</summary>
        public IReadOnlyDictionary<string, object> InputSnapshot { get; }

        /// <summary>노드 실행 시점의 데이터 출력 핀 스냅샷. 비어있을 수 있음.</summary>
        public IReadOnlyDictionary<string, object> OutputSnapshot { get; }

        /// <summary>실패한 경우의 예외 — 성공이면 null.</summary>
        public Exception Error { get; }

        public bool Success => Error == null;

        public NodeExecutionRecord(Guid nodeId, string typeId, DateTimeOffset startedAt,
                                   TimeSpan elapsed,
                                   IReadOnlyDictionary<string, object> inputSnapshot,
                                   IReadOnlyDictionary<string, object> outputSnapshot,
                                   Exception error)
        {
            NodeId = nodeId;
            TypeId = typeId;
            StartedAt = startedAt;
            Elapsed = elapsed;
            InputSnapshot = inputSnapshot ?? EmptyDict;
            OutputSnapshot = outputSnapshot ?? EmptyDict;
            Error = error;
        }

        private static readonly IReadOnlyDictionary<string, object> EmptyDict
            = new Dictionary<string, object>(0);
    }
}
