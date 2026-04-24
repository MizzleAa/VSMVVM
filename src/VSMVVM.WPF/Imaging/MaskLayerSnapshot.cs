using System;
using System.Collections.Generic;
using System.Windows;

#nullable enable
namespace VSMVVM.WPF.Imaging
{
    /// <summary>
    /// MaskLayer 전체 상태 스냅샷. 라벨별 레이어 모델 대응:
    /// - Layers: 라벨 인덱스 → 해당 라벨의 instanceMask (uint[], width*height, 0=not in this label)
    /// - Instances: 인스턴스 메타데이터
    /// - NextId: 단조증가 ID 카운터 복원용
    /// </summary>
    public sealed class MaskLayerSnapshot
    {
        public MaskLayerSnapshot(
            IReadOnlyDictionary<int, uint[]> layers,
            IReadOnlyList<InstanceRecord> instances,
            uint nextId)
        {
            Layers = layers ?? throw new ArgumentNullException(nameof(layers));
            Instances = instances ?? throw new ArgumentNullException(nameof(instances));
            NextId = nextId;
        }

        public IReadOnlyDictionary<int, uint[]> Layers { get; }
        public IReadOnlyList<InstanceRecord> Instances { get; }
        public uint NextId { get; }

        public readonly struct InstanceRecord
        {
            public InstanceRecord(uint id, int labelIndex, Rect boundingBox, int pixelCount, bool isVisible,
                IReadOnlyList<Point>? polygonPoints = null)
            {
                Id = id;
                LabelIndex = labelIndex;
                BoundingBox = boundingBox;
                PixelCount = pixelCount;
                IsVisible = isVisible;
                PolygonPoints = polygonPoints;
            }

            public uint Id { get; }
            public int LabelIndex { get; }
            public Rect BoundingBox { get; }
            public int PixelCount { get; }
            public bool IsVisible { get; }
            public IReadOnlyList<Point>? PolygonPoints { get; }
        }
    }
}