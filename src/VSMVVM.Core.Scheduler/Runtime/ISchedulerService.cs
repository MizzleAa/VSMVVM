using System;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Graph;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>
    /// 그래프 실행 서비스. RunAsync로 단발 실행, ToggleBreakpoint/Continue/StepOver로 디버거 제어.
    /// </summary>
    public interface ISchedulerService
    {
        /// <summary>주어진 그래프의 entryNodeId 노드부터 실행을 시작합니다.</summary>
        Task<ExecutionResult> RunAsync(NodeGraph graph, Guid entryNodeId, ExecutionContext context);

        /// <summary>특정 노드에 브레이크포인트를 토글.</summary>
        void ToggleBreakpoint(Guid nodeId);

        /// <summary>특정 노드의 브레이크포인트를 명시적으로 설정/해제. 토글보다 안전 — 여러 소스가 상태를 동기화할 때 사용.</summary>
        void SetBreakpoint(Guid nodeId, bool enabled);

        /// <summary>일시정지된 실행을 재개. 게이트가 없으면 무시.</summary>
        void Continue();

        /// <summary>일시정지 상태에서 한 노드만 진행 후 다시 일시정지.</summary>
        void StepOver();
    }
}
