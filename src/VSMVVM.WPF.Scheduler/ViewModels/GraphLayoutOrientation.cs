namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// 그래프 자동 정렬 및 연결선 베지어 곡선의 방향.
    /// Horizontal: exec 흐름이 좌→우 컬럼으로 전개. 베지어 컨트롤 포인트가 수평으로 펴짐.
    /// Vertical:   exec 흐름이 위→아래 행으로 전개 (Netron 스타일). 컨트롤 포인트가 수직으로 펴짐.
    /// </summary>
    public enum GraphLayoutOrientation
    {
        Horizontal = 0,
        Vertical = 1,
    }
}
