using System.Collections.Generic;

#nullable enable
namespace VSMVVM.WPF.Controls.Behaviors
{
    /// <summary>
    /// VM → MaskBehavior 로 전달되는 인스턴스 단위 작업 요청 토큰.
    /// DP 로 set 될 때마다 Behavior 가 재발동하도록 매번 새 인스턴스를 생성해서 넘긴다.
    /// </summary>
    public sealed class MaskInstanceRequest
    {
        public MaskInstanceRequest(uint instanceId)
        {
            InstanceId = instanceId;
        }

        public uint InstanceId { get; }
    }

    /// <summary>
    /// 다중 인스턴스 삭제 요청 토큰. 단일 삭제와 동일한 Undo 경로를 공유한다.
    /// </summary>
    public sealed class MaskInstancesRequest
    {
        public MaskInstancesRequest(IReadOnlyList<uint> instanceIds)
        {
            InstanceIds = instanceIds;
        }

        public IReadOnlyList<uint> InstanceIds { get; }
    }

    /// <summary>
    /// 다중 인스턴스 병합 요청 토큰. 같은 라벨 그룹끼리 하나의 ID(min)로 통합한다.
    /// </summary>
    public sealed class MaskInstancesMergeRequest
    {
        public MaskInstancesMergeRequest(IReadOnlyList<uint> instanceIds)
        {
            InstanceIds = instanceIds;
        }

        public IReadOnlyList<uint> InstanceIds { get; }
    }

    /// <summary>
    /// 단일 인스턴스의 라벨 변경 요청 토큰. MaskLayer.ChangeInstanceLabel 로 픽셀을 새 라벨 layer 로 이동.
    /// </summary>
    public sealed class MaskInstanceRelabelRequest
    {
        public MaskInstanceRelabelRequest(uint instanceId, int newLabelIndex)
        {
            InstanceId = instanceId;
            NewLabelIndex = newLabelIndex;
        }

        public uint InstanceId { get; }
        public int NewLabelIndex { get; }
    }

    /// <summary>
    /// 다중 인스턴스 일괄 라벨 변경 요청 토큰. 모든 인스턴스를 동일 newLabelIndex 로 이동.
    /// </summary>
    public sealed class MaskInstancesRelabelRequest
    {
        public MaskInstancesRelabelRequest(IReadOnlyList<uint> instanceIds, int newLabelIndex)
        {
            InstanceIds = instanceIds;
            NewLabelIndex = newLabelIndex;
        }

        public IReadOnlyList<uint> InstanceIds { get; }
        public int NewLabelIndex { get; }
    }
}