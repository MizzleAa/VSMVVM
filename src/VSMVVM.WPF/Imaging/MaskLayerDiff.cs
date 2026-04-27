using System.Collections.Generic;

#nullable enable
namespace VSMVVM.WPF.Imaging
{
    /// <summary>
    /// Stroke/Erase 한 번에 변경된 픽셀과 인스턴스 상태의 "변경분(diff)" 만 저장한다.
    /// MaskLayerSnapshot 과 달리 <c>uint[W*H]</c> 를 복제하지 않고 touched 픽셀 수에 비례하는 메모리만 사용.
    /// Undo/Redo 는 diff 를 forward/reverse 적용.
    /// </summary>
    public sealed class MaskLayerDiff
    {
        /// <summary>변경된 픽셀 엔트리. (labelIndex, pixelIndex, oldId, newId).
        /// 같은 픽셀이 stroke 중 여러 번 덮여도 최초의 oldId 만 기록되고 newId 는 최종값.</summary>
        public readonly struct PixelEntry
        {
            public PixelEntry(int labelIndex, int pixelIndex, uint oldId, uint newId)
            {
                LabelIndex = labelIndex;
                PixelIndex = pixelIndex;
                OldId = oldId;
                NewId = newId;
            }

            public int LabelIndex { get; }
            public int PixelIndex { get; }
            public uint OldId { get; }
            public uint NewId { get; }
        }

        /// <summary>RLE 압축된 픽셀 변경 run. (label, oldId, newId) 가 같고 pixelIndex 가 연속인 entries 를 한 run 으로 묶음.
        /// brush stroke 처럼 같은 (0, newId) 변환이 수백만 픽셀 연속이면 압축률 매우 높음 (1.5M entries → 수백 runs).</summary>
        public readonly struct PixelRun
        {
            public PixelRun(int labelIndex, int startIndex, int length, uint oldId, uint newId)
            {
                LabelIndex = labelIndex;
                StartIndex = startIndex;
                Length = length;
                OldId = oldId;
                NewId = newId;
            }

            public int LabelIndex { get; }
            public int StartIndex { get; }
            public int Length { get; }
            public uint OldId { get; }
            public uint NewId { get; }
        }

        /// <summary>인스턴스 추가/삭제/메타 변경을 역행 가능하게 기록.</summary>
        public sealed class InstanceDelta
        {
            public InstanceDelta(
                IReadOnlyList<MaskLayerSnapshot.InstanceRecord> before,
                IReadOnlyList<MaskLayerSnapshot.InstanceRecord> after,
                uint nextIdBefore, uint nextIdAfter)
            {
                Before = before;
                After = after;
                NextIdBefore = nextIdBefore;
                NextIdAfter = nextIdAfter;
            }

            /// <summary>stroke 시작 시점 인스턴스 전체(컬렉션 크기만큼).</summary>
            public IReadOnlyList<MaskLayerSnapshot.InstanceRecord> Before { get; }

            /// <summary>stroke 끝난 뒤 인스턴스 전체.</summary>
            public IReadOnlyList<MaskLayerSnapshot.InstanceRecord> After { get; }

            public uint NextIdBefore { get; }
            public uint NextIdAfter { get; }
        }

        public MaskLayerDiff(IReadOnlyList<PixelEntry> entries, InstanceDelta instances,
            int strokeMinX, int strokeMinY, int strokeMaxX, int strokeMaxY)
        {
            Entries = entries;
            Runs = System.Array.Empty<PixelRun>();
            Instances = instances;
            StrokeMinX = strokeMinX;
            StrokeMinY = strokeMinY;
            StrokeMaxX = strokeMaxX;
            StrokeMaxY = strokeMaxY;
        }

        /// <summary>RLE 압축된 runs 기반 생성자. Entries 는 빈 배열 (호환).</summary>
        public MaskLayerDiff(IReadOnlyList<PixelRun> runs, InstanceDelta instances,
            int strokeMinX, int strokeMinY, int strokeMaxX, int strokeMaxY)
        {
            Entries = System.Array.Empty<PixelEntry>();
            Runs = runs;
            Instances = instances;
            StrokeMinX = strokeMinX;
            StrokeMinY = strokeMinY;
            StrokeMaxX = strokeMaxX;
            StrokeMaxY = strokeMaxY;
        }

        public IReadOnlyList<PixelEntry> Entries { get; }

        /// <summary>RLE 압축된 픽셀 변경. EndDiffRecording 가 entries 를 압축해 채운다.</summary>
        public IReadOnlyList<PixelRun> Runs { get; }
        public InstanceDelta Instances { get; }

        /// <summary>변경 영역 BBox. DisplayRect 부분 갱신용.</summary>
        public int StrokeMinX { get; }
        public int StrokeMinY { get; }
        public int StrokeMaxX { get; }
        public int StrokeMaxY { get; }

        public bool HasPixelChanges => Entries.Count > 0 || Runs.Count > 0;
    }
}
