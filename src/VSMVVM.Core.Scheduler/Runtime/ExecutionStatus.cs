namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>한 번의 그래프 실행 결과 상태.</summary>
    public enum ExecutionStatus
    {
        Running,
        Completed,
        Failed,
        Cancelled,
    }

    /// <summary>디버거 단계 모드 (Phase 3a에선 Run/Paused만 의미 있음; StepInto/Over는 Phase 7).</summary>
    public enum DebugStepMode
    {
        Run,
        StepInto,
        StepOver,
        Paused,
    }
}
