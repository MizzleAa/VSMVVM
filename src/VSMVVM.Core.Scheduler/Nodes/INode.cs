using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// 그래프의 단위 노드. 핀 컬렉션과 실행/평가 메소드를 노출합니다.
    /// </summary>
    public interface INode
    {
        /// <summary>그래프 내 인스턴스 식별자.</summary>
        Guid Id { get; }

        /// <summary>NodeMetadata.TypeId와 일치하는 타입 식별자(직렬화용).</summary>
        string TypeId { get; }

        /// <summary>이 노드의 모든 핀 (입력/출력, Exec/Data 통합).</summary>
        IReadOnlyList<IPin> Pins { get; }

        /// <summary>
        /// 노드 실행. exec-in으로 진입 시 호출되며, 발화할 exec-out 핀 id를 반환합니다.
        /// Phase 1에서는 시그니처만 정의되며, Phase 3a (SchedulerService)에서 실제로 호출됩니다.
        /// </summary>
        Task<ExecutionFlow> ExecuteAsync(ExecutionContext context);

        /// <summary>
        /// 데이터 출력 평가. 하류 노드가 input pull 시 호출됩니다.
        /// 기본 구현은 ExecuteAsync를 한 번 호출(캐시는 컨텍스트가 담당).
        /// </summary>
        Task EvaluateAsync(ExecutionContext context);
    }
}
