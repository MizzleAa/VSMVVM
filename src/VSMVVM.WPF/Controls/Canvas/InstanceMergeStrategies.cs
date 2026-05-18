using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 같은 라벨 instance 가 겹치면 자동 통합. Segmentation 기본 정책.
    /// EndStroke: stroke 영역의 옛 ID 들과 새 stroke ID 를 하나로 remap (가장 작은 ID 로 통합) + 나머지 instance 제거.
    /// ResampleInstance: 이동한 instance 가 침범한 옛 instance 들을 흡수 (remap + remove).
    /// </summary>
    public sealed class MergeOnOverlapInstanceMergeStrategy : IInstanceMergeStrategy
    {
        public static readonly MergeOnOverlapInstanceMergeStrategy Instance = new();

        private MergeOnOverlapInstanceMergeStrategy() { }

        public uint OnEndStroke(IInstanceMergeContext ctx)
        {
            if (ctx.OverlappedInstanceIds.Count == 0)
            {
                ctx.RecomputeInstanceMetadata(ctx.TentativeId);
                return ctx.TentativeId;
            }

            // finalId = 모든 후보(옛 + tentativeId) 중 최소. 가장 오래된 ID 로 통합 — 안정적 식별자.
            var allIds = new HashSet<uint>(ctx.OverlappedInstanceIds) { ctx.TentativeId };
            uint finalId = allIds.Min();
            var remap = new HashSet<uint>(allIds);
            remap.Remove(finalId);
            ctx.RemapInstanceIds(remap, finalId);
            // remap 대상이 된 instance(옛 ID + tentativeId 중 finalId 가 아닌 것) 모두 컬렉션에서 제거.
            foreach (var id in remap) ctx.RemoveInstance(id);
            ctx.RecomputeInstanceMetadata(finalId);
            return finalId;
        }

        public void OnResampleOverlap(IInstanceMergeContext ctx)
        {
            if (ctx.OverlappedInstanceIds.Count > 0)
            {
                ctx.RemapInstanceIds(ctx.OverlappedInstanceIds, ctx.TentativeId);
                foreach (var id in ctx.OverlappedInstanceIds) ctx.RemoveInstance(id);
                ctx.ClearPolygonContours(ctx.TentativeId);
            }
            ctx.RecomputeInstanceMetadata(ctx.TentativeId);
        }
    }

    /// <summary>
    /// 같은 라벨 instance 가 겹쳐도 각자 독립 유지. ObjectDetection 처럼 bbox 단위 모델에 사용.
    /// EndStroke: 새 instance 의 BoundingBox 를 strokeBounds 로 강제 + 옛 instance 들의 BoundingBox 는 freeze.
    /// ResampleInstance: 이동한 instance 의 BoundingBox 갱신 + 옛 instance 들의 BoundingBox 보존 (흡수 안 함).
    /// 픽셀 자체는 새 ID 가 덮을 수 있음 — fill 표시는 정상이고 instance 식별만 독립 유지.
    /// </summary>
    public sealed class IndependentInstancesInstanceMergeStrategy : IInstanceMergeStrategy
    {
        public static readonly IndependentInstancesInstanceMergeStrategy Instance = new();

        private IndependentInstancesInstanceMergeStrategy() { }

        public uint OnEndStroke(IInstanceMergeContext ctx)
        {
            // 모든 옛 instance (TentativeId 제외) 의 BoundingBox 스냅샷.
            // OverlappedInstanceIds 만 보면 stroke 가 같은 라벨 다른 instance 픽셀을 덮은 경우만 잡힘 —
            // BBox 만 겹치고 픽셀이 안 닿은 경우엔 누락. drag-separate L-shape bug 는 step 1 픽셀 클리어 단계에서
            // 옛 instance BBox 안 픽셀이 사라지는 흐름이라 BBox 겹침까지 봐야 함.
            var preserveBoxes = new Dictionary<uint, Rect>();
            foreach (var otherId in ctx.EnumerateInstanceIds())
            {
                if (otherId == ctx.TentativeId) continue;
                var box = ctx.GetInstanceBoundingBox(otherId);
                if (!box.IsEmpty) preserveBoxes[otherId] = box;
            }

            ctx.RecomputeInstanceMetadata(ctx.TentativeId);

            // 새 instance 의 BoundingBox 를 strokeBounds 로 강제 — render 가 BBox 단위 fill 이라 사각형 표시.
            if (!ctx.StrokeBounds.IsEmpty) ctx.SetInstanceBoundingBox(ctx.TentativeId, ctx.StrokeBounds);

            // 옛 instance 들 — BoundingBox freeze + 픽셀 마스크 reclaim.
            // ReclaimBoundingBoxPixels 는 옛 instance BBox 안 픽셀 중 자기 ID 가 아닌 자리를 자기 ID 로 set —
            // 새 stroke 가 자기 영역을 덮은 픽셀들이 옛 ID 로 복원되어 COCO RLE 저장 시 L 자 손상 방지.
            // 한 픽셀은 한 ID 만 가질 수 있어 새 instance 는 자기 BBox 일부 픽셀을 잃지만, render 는 BBox fill 이라 시각 동일.
            foreach (var kv in preserveBoxes)
            {
                ctx.SetInstanceBoundingBox(kv.Key, kv.Value);
                ctx.ReclaimBoundingBoxPixels(kv.Key);
                ctx.RecomputeInstanceMetadata(kv.Key);
                ctx.SetInstanceBoundingBox(kv.Key, kv.Value);
            }
            return ctx.TentativeId;
        }

        public void OnResampleOverlap(IInstanceMergeContext ctx)
        {
            // 모든 옛 instance (TentativeId 제외) 의 BoundingBox 스냅샷.
            var preserveBoxes = new Dictionary<uint, Rect>();
            foreach (var otherId in ctx.EnumerateInstanceIds())
            {
                if (otherId == ctx.TentativeId) continue;
                var box = ctx.GetInstanceBoundingBox(otherId);
                if (!box.IsEmpty) preserveBoxes[otherId] = box;
            }

            ctx.RecomputeInstanceMetadata(ctx.TentativeId);
            // 이동한 instance 의 BoundingBox = 새 위치 (StrokeBounds) — render 가 이 사각형을 fill.
            if (!ctx.StrokeBounds.IsEmpty)
                ctx.SetInstanceBoundingBox(ctx.TentativeId, ctx.StrokeBounds);

            // 옛 instance 들 — BoundingBox freeze + 픽셀 마스크 reclaim (drag-separate L-shape bug 방지).
            foreach (var kv in preserveBoxes)
            {
                ctx.SetInstanceBoundingBox(kv.Key, kv.Value);
                ctx.ReclaimBoundingBoxPixels(kv.Key);
                ctx.RecomputeInstanceMetadata(kv.Key);
                ctx.SetInstanceBoundingBox(kv.Key, kv.Value);
            }
        }
    }
}
