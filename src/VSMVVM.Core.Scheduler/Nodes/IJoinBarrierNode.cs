namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// JoinNode 처럼 다수의 exec-in 도착을 카운트해 마지막 도달자만 downstream 을 이어가야 하는 노드가 구현.
    /// SchedulerService 는 이 marker 를 감지하면 실행 결과와 무관하게 counter 증가 · gating 을 수행한다.
    /// <para>
    /// <see cref="ExpectedArrivals"/> 는 Join 이 대기할 in-핀 개수 (연결된 것만 셈).
    /// </para>
    /// </summary>
    public interface IJoinBarrierNode
    {
        /// <summary>대기할 총 arrival 수. SchedulerService 가 그래프 연결을 참고해 자동 산정도 가능하지만
        /// 노드 자체가 명시적으로 알려주면 우선.</summary>
        int ExpectedArrivals { get; }
    }
}