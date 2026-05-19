using System.Collections.Generic;
using System.Windows;

namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// MaskLayer 의 같은 라벨 instance 가 stroke / 이동 시점에 다른 instance 와 겹쳤을 때의 처리 정책 전략.
    /// MaskLayer 가 EndStroke / ResampleInstance 직후 hook 호출. VSMVVM 은 구체 도메인 (ObjectDetection /
    /// Segmentation 등) 을 모르고, 호출 측이 적절한 구현체를 <see cref="MaskLayer.MergeStrategy"/> 로 주입.
    /// 표준 구현은 <see cref="MergeOnOverlapInstanceMergeStrategy"/> 와 <see cref="IndependentInstancesInstanceMergeStrategy"/>.
    /// </summary>
    public interface IInstanceMergeStrategy
    {
        /// <summary>EndStroke 직후 호출. stroke 가 침범한 옛 instance 들에 대한 처리 정책 적용.
        /// 반환값은 최종 instance ID — 병합 정책이면 finalId, 독립 정책이면 tentativeId.</summary>
        uint OnEndStroke(IInstanceMergeContext ctx);

        /// <summary>ResampleInstance (instance 이동/리사이즈) 직후 호출. 이동한 instance 가 침범한 옛 instance 들 처리.</summary>
        void OnResampleOverlap(IInstanceMergeContext ctx);

        /// <summary>instance 더블클릭 시 vertex 편집 모드 진입을 허용할지.
        /// OD (IndependentInstances) 같이 bbox 사각 자체가 의미인 모델은 false → 더블클릭 무반응.
        /// 기본 구현은 true 반환 (회귀 zero — Seg/기존 동작).</summary>
        bool AllowVertexEdit => true;

        /// <summary>ResampleInstance 가 nearest-neighbor 리샘플 대신 newBBox 사각 전체를 자기 ID 로 채워야 할지.
        /// OD 의 bbox 사각 마스크처럼 사각 자체가 정보이고 sourceBits 의 가장자리 픽셀 누락이 잔존 픽셀로 보이는 케이스 fix.
        /// true 면 MaskLayer.ResampleInstance 가 step 2 를 newBBox 전체 fill 로 대체.
        /// 기본 구현은 false 반환 (회귀 zero — Seg 의 픽셀 정밀 의미 유지).</summary>
        bool RewriteRectangleOnResample => false;
    }

    /// <summary>
    /// Strategy 가 MaskLayer 의 internal state 를 조작할 수 있도록 노출하는 컨텍스트.
    /// 구현체는 이 컨텍스트의 helper 메서드들로 픽셀 remap / instance 추가-제거 / BoundingBox 갱신을 처리한다.
    /// MaskLayer 내부에서만 컨텍스트를 생성하므로 외부 구현 불필요.
    /// </summary>
    public interface IInstanceMergeContext
    {
        /// <summary>처리 대상 라벨 인덱스.</summary>
        int LabelIndex { get; }

        /// <summary>EndStroke 의 경우 새로 시작된 stroke ID. ResampleInstance 의 경우 이동한 instance ID.</summary>
        uint TentativeId { get; }

        /// <summary>EndStroke 의 stroke 영역 또는 ResampleInstance 의 새 BoundingBox.</summary>
        Rect StrokeBounds { get; }

        /// <summary>stroke / 이동 영역에서 만난 다른 instance ID 들. tentativeId 자기 자신은 제외.</summary>
        IReadOnlyCollection<uint> OverlappedInstanceIds { get; }

        // ── Helper ──
        /// <summary>해당 라벨 레이어에서 <paramref name="from"/> ID 들을 <paramref name="toId"/> 로 일괄 remap.</summary>
        void RemapInstanceIds(IEnumerable<uint> from, uint toId);

        /// <summary>_instances 컬렉션에서 ID 제거. mask 픽셀은 건드리지 않으므로 호출 전 RemapInstanceIds 필요.</summary>
        void RemoveInstance(uint id);

        /// <summary>해당 ID 의 PixelCount / BoundingBox 를 mask 픽셀로부터 재계산.</summary>
        void RecomputeInstanceMetadata(uint id);

        /// <summary>해당 ID 의 BoundingBox 를 명시 set (RecomputeInstanceMetadata 의 결과를 덮어쓸 때 사용).</summary>
        void SetInstanceBoundingBox(uint id, Rect bbox);

        /// <summary>해당 ID 의 현재 BoundingBox 조회.</summary>
        Rect GetInstanceBoundingBox(uint id);

        /// <summary>해당 ID 의 PolygonContours 무효화 (병합 등으로 mask 와 어긋났을 때).</summary>
        void ClearPolygonContours(uint id);

        /// <summary>해당 라벨 레이어의 모든 instance ID 들을 열거. Strategy 가 layer 전체 instance 를 순회할 때 사용.</summary>
        IEnumerable<uint> EnumerateInstanceIds();

        /// <summary>해당 ID 의 BoundingBox 안에서 ID=0 (빈 픽셀) 인 자리만 자기 ID 로 채움.
        /// IndependentInstances 모드에서 ResampleInstance 후 옛 instance 의 BBox 영역이 자기 ID 로 복원되도록 사용.</summary>
        void FillEmptyPixelsInBoundingBox(uint id);

        /// <summary>해당 ID 의 BoundingBox 안 픽셀을 모두 자기 ID 로 set — 다른 인스턴스가 이미 차지한 픽셀도 빼앗는다.
        /// IndependentInstances 모드의 freeze 의도 (BBox 안은 항상 자기 영역) 를 픽셀 마스크 레벨에서 보장.
        /// 같은 라벨 다른 인스턴스 BBox 와 겹치는 자리는 호출 순서 (낮은 Id 먼저, 큰 Id 가 위) 로 결정된다.</summary>
        void ReclaimBoundingBoxPixels(uint id);
    }
}
