using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>
    /// 노드 id별 실행 시간 누적 통계 (Count / Total / Min / Max / Mean / Last).
    /// SchedulerService가 자동 채우지는 않으며, 호출자가 NodeExitedMessage 수신 시 <see cref="Record"/>를 호출하여 누적.
    /// 인스턴스가 IMessenger 구독을 직접 책임지지 않는 이유는 Core가 IMessenger에 의존하지 않기 위함 (테스트 용이성).
    /// </summary>
    public sealed class ProfilingStats
    {
        private readonly ConcurrentDictionary<Guid, NodeProfile> _byNode = new();

        /// <summary>NodeExitedMessage 수신 시 호출. nodeId, elapsed 만 필요.</summary>
        public void Record(Guid nodeId, TimeSpan elapsed)
        {
            _byNode.AddOrUpdate(nodeId,
                addValueFactory: _ => NodeProfile.First(elapsed),
                updateValueFactory: (_, prev) => prev.With(elapsed));
        }

        /// <summary>특정 노드의 통계 스냅샷. 미관측 노드면 null.</summary>
        public NodeProfile? Get(Guid nodeId) =>
            _byNode.TryGetValue(nodeId, out var p) ? p : (NodeProfile?)null;

        /// <summary>모든 관측 노드의 통계 스냅샷.</summary>
        public IReadOnlyDictionary<Guid, NodeProfile> Snapshot() =>
            new Dictionary<Guid, NodeProfile>(_byNode);

        public void Clear() => _byNode.Clear();
    }

    /// <summary>한 노드의 누적 실행 시간 통계. 불변 (with-update 패턴).</summary>
    public readonly struct NodeProfile : IEquatable<NodeProfile>
    {
        public int Count { get; }
        public TimeSpan TotalElapsed { get; }
        public TimeSpan Min { get; }
        public TimeSpan Max { get; }
        public TimeSpan Last { get; }

        public TimeSpan Mean => Count == 0
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(TotalElapsed.Ticks / Count);

        public NodeProfile(int count, TimeSpan total, TimeSpan min, TimeSpan max, TimeSpan last)
        {
            Count = count;
            TotalElapsed = total;
            Min = min;
            Max = max;
            Last = last;
        }

        public static NodeProfile First(TimeSpan elapsed) =>
            new NodeProfile(count: 1, total: elapsed, min: elapsed, max: elapsed, last: elapsed);

        public NodeProfile With(TimeSpan elapsed) =>
            new NodeProfile(
                count: Count + 1,
                total: TotalElapsed + elapsed,
                min: elapsed < Min ? elapsed : Min,
                max: elapsed > Max ? elapsed : Max,
                last: elapsed);

        public bool Equals(NodeProfile other) =>
            Count == other.Count && TotalElapsed == other.TotalElapsed
            && Min == other.Min && Max == other.Max && Last == other.Last;

        public override bool Equals(object obj) => obj is NodeProfile p && Equals(p);

        public override int GetHashCode()
        {
            // netstandard2.0은 System.HashCode 미지원 — 수동 해시
            unchecked
            {
                int h = 17;
                h = h * 31 + Count.GetHashCode();
                h = h * 31 + TotalElapsed.GetHashCode();
                h = h * 31 + Min.GetHashCode();
                h = h * 31 + Max.GetHashCode();
                h = h * 31 + Last.GetHashCode();
                return h;
            }
        }
    }
}
