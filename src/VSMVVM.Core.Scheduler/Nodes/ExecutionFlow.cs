using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// 노드 실행 결과로 발화될 exec-output 핀의 id 목록을 반환합니다.
    /// Halt = 실행 중단 (End 노드 또는 미발화).
    /// Continue("Then") = 단일 exec-out 발화.
    /// Continue("Then0","Then1") = 다중 발화(Sequence 등). 발화 핀 × 연결 곱집합으로 다음 노드 결정.
    /// </summary>
    public readonly struct ExecutionFlow : IEquatable<ExecutionFlow>
    {
        private static readonly IReadOnlyList<string> EmptyList = Array.Empty<string>();

        // 백킹 필드를 직접 노출하지 않고 프로퍼티에서 null fallback 처리:
        // default(ExecutionFlow)는 _firedPinIds == null 이므로 항상 EmptyList로 회귀.
        private readonly IReadOnlyList<string> _firedPinIds;

        public IReadOnlyList<string> FiredPinIds => _firedPinIds ?? EmptyList;

        public bool IsHalt => _firedPinIds == null || _firedPinIds.Count == 0;

        private ExecutionFlow(IReadOnlyList<string> firedPinIds)
        {
            _firedPinIds = firedPinIds ?? EmptyList;
        }

        public static ExecutionFlow Halt => default;

        public static ExecutionFlow Continue(params string[] execOutputPinIds)
        {
            if (execOutputPinIds == null || execOutputPinIds.Length == 0)
            {
                return Halt;
            }
            return new ExecutionFlow(execOutputPinIds);
        }

        public bool Equals(ExecutionFlow other)
        {
            if (IsHalt && other.IsHalt) return true;
            if (IsHalt != other.IsHalt) return false;
            if (FiredPinIds.Count != other.FiredPinIds.Count) return false;
            for (int i = 0; i < FiredPinIds.Count; i++)
            {
                if (FiredPinIds[i] != other.FiredPinIds[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is ExecutionFlow other && Equals(other);

        public override int GetHashCode()
        {
            if (IsHalt) return 0;
            int hash = 17;
            for (int i = 0; i < FiredPinIds.Count; i++)
            {
                hash = unchecked(hash * 31 + (FiredPinIds[i]?.GetHashCode() ?? 0));
            }
            return hash;
        }

        public static bool operator ==(ExecutionFlow left, ExecutionFlow right) => left.Equals(right);
        public static bool operator !=(ExecutionFlow left, ExecutionFlow right) => !left.Equals(right);
    }
}
