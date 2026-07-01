namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// 노드가 가변 핀 N 쌍 (또는 N 개) 을 가지는 경우 구현. UI 인스펙터가 이걸 감지하면 +/- 버튼 노출 →
    /// 사용자가 핀 수를 늘리거나 줄일 수 있다.
    /// <para>
    /// 구현 예: <see cref="BuiltIn.LogNode"/> (ArgCount), <see cref="BuiltIn.ParameterGroupNode"/> (PinCount).
    /// </para>
    /// </summary>
    public interface IDynamicPinCountNode
    {
        /// <summary>현재 핀 수 (해석은 노드별 — 쌍/개수 등).</summary>
        int DynamicPinCount { get; set; }

        /// <summary>UI 표시용 라벨 (예: "Arguments", "Pin pairs"). null/빈 값이면 "Pins" 같은 generic.</summary>
        string DynamicPinCountLabel { get; }

        /// <summary>허용 최솟값. UI 가 이보다 아래로 감소 못 하게 함.</summary>
        int MinDynamicPinCount { get; }

        /// <summary>허용 최댓값. UI 가 이보다 위로 증가 못 하게 함. 무한대 의도면 int.MaxValue.</summary>
        int MaxDynamicPinCount { get; }
    }
}
