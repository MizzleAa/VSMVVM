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
}