using System;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>그래프 실행 중 데이터 핀 풀에서 순환 의존성이 감지되었습니다.</summary>
    public sealed class CyclicDataDependencyException : Exception
    {
        public Guid NodeId { get; }
        public string PinId { get; }

        public CyclicDataDependencyException(Guid nodeId, string pinId)
            : base($"Cyclic data dependency detected at node {nodeId}, pin '{pinId}'.")
        {
            NodeId = nodeId;
            PinId = pinId;
        }
    }

    /// <summary>그래프 실행이 MaxNodesExecuted 한도를 초과했습니다 (무한 루프 가드).</summary>
    public sealed class SchedulerOverflowException : Exception
    {
        public int Limit { get; }

        public SchedulerOverflowException(int limit)
            : base($"Scheduler exceeded MaxNodesExecuted limit ({limit}). Possible infinite loop.")
        {
            Limit = limit;
        }
    }

    /// <summary>노드가 PerNodeTimeoutMs 또는 [Node(TimeoutMs=...)] 한도를 초과했습니다.</summary>
    public sealed class NodeTimeoutException : Exception
    {
        public Guid NodeId { get; }
        public int TimeoutMs { get; }

        public NodeTimeoutException(Guid nodeId, int timeoutMs)
            : base($"Node {nodeId} exceeded execution timeout of {timeoutMs} ms.")
        {
            NodeId = nodeId;
            TimeoutMs = timeoutMs;
        }
    }

    /// <summary>그래프 실행 중 메모리 예산을 초과해 graceful 중단되었습니다.</summary>
    public sealed class GraphMemoryBudgetExceededException : Exception
    {
        public long BudgetBytes { get; }
        public long ObservedBytes { get; }

        public GraphMemoryBudgetExceededException(long budgetBytes, long observedBytes)
            : base($"Graph exceeded memory budget of {budgetBytes} bytes (observed {observedBytes}).")
        {
            BudgetBytes = budgetBytes;
            ObservedBytes = observedBytes;
        }
    }

    /// <summary>AssertNode 또는 RangeAssertNode 의 조건이 거짓이어서 실행이 중단되었습니다.</summary>
    public sealed class AssertionFailedException : Exception
    {
        public Guid NodeId { get; }

        public AssertionFailedException(Guid nodeId, string message)
            : base(message)
        {
            NodeId = nodeId;
        }
    }
}
