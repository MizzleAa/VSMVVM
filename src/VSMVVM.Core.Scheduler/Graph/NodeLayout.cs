namespace VSMVVM.Core.Scheduler.Graph
{
    /// <summary>그래프 내 노드의 화면 좌표(에디터 전용; 실행에는 영향 없음).</summary>
    public readonly struct NodeLayout
    {
        public double X { get; }
        public double Y { get; }

        public NodeLayout(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
