using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>
    /// 완료된 ExecutionRun 들의 보관소. 기본 구현은 메모리(최근 N 개) — 사용자는 DI 로 디스크/원격 sink 로 교체 가능.
    /// SchedulerService 가 RunAsync 종료 시 자동 push (Messenger 와 마찬가지 방식 — null 이면 skip).
    /// </summary>
    public interface IExecutionHistoryStore
    {
        /// <summary>완료된 run 을 보관. 정책에 따라 가장 오래된 항목 제거 가능.</summary>
        void Add(ExecutionRun run);

        /// <summary>현재 보관 중인 모든 run (최신이 마지막일 수도, 처음일 수도 있음 — 구현 책임).</summary>
        IReadOnlyList<ExecutionRun> GetAll();

        /// <summary>전체 삭제.</summary>
        void Clear();

        /// <summary>새 run 이 Add 될 때마다 발화. UI 가 ObservableCollection 동기화 용으로 구독.</summary>
        event EventHandler<ExecutionRun> RunAdded;
    }
}
