namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// SchedulerService 가 이 노드에서 다중 exec-out 이 발화되면 각 브랜치를 병렬 Task 로 분기하도록 지시하는 marker.
    /// Sequence 같이 다중 발화하지만 직렬 실행이 의도인 노드는 이 인터페이스를 구현하지 않는다.
    /// </summary>
    public interface IParallelForkNode
    {
    }
}