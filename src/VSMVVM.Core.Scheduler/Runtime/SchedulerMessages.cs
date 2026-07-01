using System;
using System.Collections.Generic;
using VSMVVM.Core.MVVM;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>노드 실행 직전 발화. 디버거/에디터가 IsExecuting 하이라이트에 사용.</summary>
    public sealed class NodeEnteringMessage : MessageBase
    {
        public Guid RunId { get; }
        public Guid GraphId { get; }
        public Guid NodeId { get; }
        public string NodeTypeId { get; }

        public NodeEnteringMessage(Guid runId, Guid graphId, Guid nodeId, string nodeTypeId)
        {
            RunId = runId;
            GraphId = graphId;
            NodeId = nodeId;
            NodeTypeId = nodeTypeId;
        }
    }

    /// <summary>노드 실행 직후 발화. Elapsed로 프로파일링, Inputs/Outputs로 인스펙션.
    /// Inputs/Outputs는 노드 실행 시점의 ExecutionContext.DataCache 에서 캡처한 immutable 스냅샷.
    /// 미연결 입력 핀이나 SetOutput으로 채워지지 않은 출력 핀은 키가 없음 (sparse).</summary>
    public sealed class NodeExitedMessage : MessageBase
    {
        private static readonly IReadOnlyDictionary<string, object> EmptySnapshot
            = new Dictionary<string, object>(0);

        public Guid RunId { get; }
        public Guid GraphId { get; }
        public Guid NodeId { get; }
        public string NodeTypeId { get; }
        public bool Success { get; }
        public TimeSpan Elapsed { get; }
        public Exception Error { get; }

        /// <summary>노드 실행 종료 시점의 데이터 입력 핀별 값 스냅샷 (pinId → value).</summary>
        public IReadOnlyDictionary<string, object> Inputs { get; }

        /// <summary>노드 실행 종료 시점의 데이터 출력 핀별 값 스냅샷 (pinId → value).</summary>
        public IReadOnlyDictionary<string, object> Outputs { get; }

        public NodeExitedMessage(Guid runId, Guid graphId, Guid nodeId, string nodeTypeId,
                                 bool success, TimeSpan elapsed, Exception error,
                                 IReadOnlyDictionary<string, object> inputs = null,
                                 IReadOnlyDictionary<string, object> outputs = null)
        {
            RunId = runId;
            GraphId = graphId;
            NodeId = nodeId;
            NodeTypeId = nodeTypeId;
            Success = success;
            Elapsed = elapsed;
            Error = error;
            Inputs = inputs ?? EmptySnapshot;
            Outputs = outputs ?? EmptySnapshot;
        }
    }

    /// <summary>브레이크포인트가 발동되어 노드 직전에서 일시정지함.</summary>
    public sealed class BreakpointHitMessage : MessageBase
    {
        public Guid RunId { get; }
        public Guid GraphId { get; }
        public Guid NodeId { get; }

        public BreakpointHitMessage(Guid runId, Guid graphId, Guid nodeId)
        {
            RunId = runId;
            GraphId = graphId;
            NodeId = nodeId;
        }
    }

    /// <summary>그래프 실행이 완료(성공/실패/취소)되었습니다.</summary>
    public sealed class GraphCompletedMessage : MessageBase
    {
        public Guid GraphId { get; }
        public ExecutionResult Result { get; }

        public GraphCompletedMessage(Guid graphId, ExecutionResult result)
        {
            GraphId = graphId;
            Result = result;
        }
    }

    /// <summary>시스템 가드(MaxNodes/Timeout/Memory)가 발동되었습니다.</summary>
    public sealed class GuardTriggeredMessage : MessageBase
    {
        public Guid RunId { get; }
        public Guid GraphId { get; }
        public string GuardKind { get; } // "MaxNodes", "Timeout", "Memory"
        public string Message { get; }

        public GuardTriggeredMessage(Guid runId, Guid graphId, string guardKind, string message)
        {
            RunId = runId;
            GraphId = graphId;
            GuardKind = guardKind;
            Message = message;
        }
    }
}
