using System;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Runtime;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Runtime
{
    public class ProfilingStatsTests
    {
        [Fact]
        public void Get_UnobservedNode_ReturnsNull()
        {
            var stats = new ProfilingStats();
            stats.Get(Guid.NewGuid()).Should().BeNull();
        }

        [Fact]
        public void Record_First_SetsAllMetricsToSameValue()
        {
            var stats = new ProfilingStats();
            var id = Guid.NewGuid();

            stats.Record(id, TimeSpan.FromMilliseconds(50));

            var p = stats.Get(id).Value;
            p.Count.Should().Be(1);
            p.TotalElapsed.Should().Be(TimeSpan.FromMilliseconds(50));
            p.Min.Should().Be(TimeSpan.FromMilliseconds(50));
            p.Max.Should().Be(TimeSpan.FromMilliseconds(50));
            p.Last.Should().Be(TimeSpan.FromMilliseconds(50));
            p.Mean.Should().Be(TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public void Record_Multiple_AccumulatesMinMaxMeanTotal()
        {
            var stats = new ProfilingStats();
            var id = Guid.NewGuid();

            stats.Record(id, TimeSpan.FromMilliseconds(20));
            stats.Record(id, TimeSpan.FromMilliseconds(80));
            stats.Record(id, TimeSpan.FromMilliseconds(50));

            var p = stats.Get(id).Value;
            p.Count.Should().Be(3);
            p.TotalElapsed.Should().Be(TimeSpan.FromMilliseconds(150));
            p.Min.Should().Be(TimeSpan.FromMilliseconds(20));
            p.Max.Should().Be(TimeSpan.FromMilliseconds(80));
            p.Last.Should().Be(TimeSpan.FromMilliseconds(50));
            p.Mean.Should().Be(TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public void Record_DifferentNodes_AreIndependent()
        {
            var stats = new ProfilingStats();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();

            stats.Record(a, TimeSpan.FromMilliseconds(10));
            stats.Record(b, TimeSpan.FromMilliseconds(200));
            stats.Record(a, TimeSpan.FromMilliseconds(30));

            stats.Get(a).Value.Count.Should().Be(2);
            stats.Get(a).Value.Last.Should().Be(TimeSpan.FromMilliseconds(30));
            stats.Get(b).Value.Count.Should().Be(1);
            stats.Get(b).Value.Last.Should().Be(TimeSpan.FromMilliseconds(200));
        }

        [Fact]
        public void Snapshot_ReturnsAllObservedNodes()
        {
            var stats = new ProfilingStats();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            stats.Record(a, TimeSpan.FromMilliseconds(5));
            stats.Record(b, TimeSpan.FromMilliseconds(10));

            var snap = stats.Snapshot();
            snap.Should().HaveCount(2);
            snap.Keys.Should().Contain(new[] { a, b });
        }

        [Fact]
        public void Clear_RemovesAllStats()
        {
            var stats = new ProfilingStats();
            var id = Guid.NewGuid();
            stats.Record(id, TimeSpan.FromMilliseconds(1));

            stats.Clear();

            stats.Get(id).Should().BeNull();
            stats.Snapshot().Should().BeEmpty();
        }

        [Fact]
        public void NodeProfile_Equality_StructSemantics()
        {
            var p1 = new NodeProfile(2, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(40),
                                     TimeSpan.FromMilliseconds(60), TimeSpan.FromMilliseconds(60));
            var p2 = new NodeProfile(2, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(40),
                                     TimeSpan.FromMilliseconds(60), TimeSpan.FromMilliseconds(60));
            var p3 = p2.With(TimeSpan.FromMilliseconds(20));

            p1.Should().Be(p2);
            p1.GetHashCode().Should().Be(p2.GetHashCode());
            p1.Should().NotBe(p3);
        }
    }
}
