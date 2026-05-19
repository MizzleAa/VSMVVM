namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// MaskLayer 의 같은 라벨 instance 겹침 시 자동 통합 정책.
    /// 라벨링 ModelType 별 데이터 모델 차이를 표현하기 위한 핵심 분기 — Segmentation 은 픽셀 마스크 단위라
    /// 자동 병합이 자연스럽고, ObjectDetection 같은 bbox 단위 모델은 각 instance 가 독립 유지되어야 한다.
    /// </summary>
    public enum InstanceMergePolicy
    {
        /// <summary>같은 라벨의 stroke / 이동이 다른 instance 와 겹치면 자동으로 한 instance 로 통합.
        /// Segmentation 기본 동작. EndStroke 가 겹친 옛 ID 들을 새 ID 로 remap + 옛 instance 제거,
        /// ResampleInstance 가 새 위치에서 만난 다른 instance 흡수.</summary>
        MergeOnOverlap = 0,

        /// <summary>겹쳐도 instance 가 독립 유지. ObjectDetection 처럼 bbox 단위 모델용.
        /// WritePixelToLayer 가 stroke 픽셀을 옛 instance 위에 덮어쓰지 않음 +
        /// EndStroke 의 자동 병합 분기 차단 + ResampleInstance 의 흡수 분기 차단.</summary>
        IndependentInstances = 1,
    }
}
