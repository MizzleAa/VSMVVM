using System.Collections.Generic;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.Core.Scheduler.Graph
{
    /// <summary>
    /// N:M 연결 규칙. 핀별 카디널리티:
    ///   Exec-Output / Exec-Input / Data-Output: 무제한 (N:M 허용)
    ///   Data-Input: 정확히 1개 (한 입력은 한 소스만 — pull 의미 명확성)
    ///
    /// data-input에 기존 연결이 있는 상태에서 새로 연결하면, 기존 연결을 자동 제거 후 신규 추가 (Blueprint UX).
    /// </summary>
    public static class ConnectionRules
    {
        /// <summary>
        /// 새 연결이 허용되는지 확인. PinCompatibility를 통과하고, 카디널리티 제약을 위반하지 않아야 한다.
        /// data-input에 기존 연결이 있으면 false가 아니라 true이지만, <paramref name="conflictingConnections"/>에
        /// 자동 제거 대상 연결을 채워 반환한다.
        /// </summary>
        public static bool CanConnect(
            IReadOnlyList<NodeConnection> existing,
            IPin source,
            IPin target,
            out string reason,
            out IReadOnlyList<NodeConnection> conflictingConnections)
        {
            conflictingConnections = System.Array.Empty<NodeConnection>();

            if (!PinCompatibility.CanConnect(source, target, out reason))
            {
                return false;
            }

            if (target.Kind == PinKind.Data && target.Direction == PinDirection.Input)
            {
                var conflicts = new List<NodeConnection>();
                for (int i = 0; i < existing.Count; i++)
                {
                    var c = existing[i];
                    if (c.TargetNodeId == target.Owner.Id && c.TargetPinId == target.Id)
                    {
                        conflicts.Add(c);
                    }
                }
                if (conflicts.Count > 0)
                {
                    conflictingConnections = conflicts;
                }
            }

            return true;
        }
    }
}
