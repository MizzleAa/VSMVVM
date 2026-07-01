using System.Collections.Generic;
using FluentAssertions;
using VSMVVM.WPF.Imaging;
using Xunit;

namespace VSMVVM.WPF.Tests.Imaging
{
    public class MaskLayerDiffTests
    {
        private static MaskLayerDiff.InstanceDelta EmptyInstanceDelta() =>
            new MaskLayerDiff.InstanceDelta(
                new List<MaskLayerSnapshot.InstanceRecord>(),
                new List<MaskLayerSnapshot.InstanceRecord>(),
                nextIdBefore: 1u,
                nextIdAfter: 1u);

        // ── PixelEntry 생성자 / 프로퍼티 ────────────────────────────

        [Fact]
        public void PixelEntry_WhenConstructed_ShouldHaveCorrectProperties()
        {
            // Arrange / Act
            var entry = new MaskLayerDiff.PixelEntry(
                labelIndex: 2, pixelIndex: 500, oldId: 0u, newId: 3u);

            // Assert
            entry.LabelIndex.Should().Be(2);
            entry.PixelIndex.Should().Be(500);
            entry.OldId.Should().Be(0u);
            entry.NewId.Should().Be(3u);
        }

        // ── PixelRun 생성자 / 프로퍼티 ──────────────────────────────

        [Fact]
        public void PixelRun_WhenConstructed_ShouldHaveCorrectProperties()
        {
            // Arrange / Act
            var run = new MaskLayerDiff.PixelRun(
                labelIndex: 1, startIndex: 100, length: 50, oldId: 0u, newId: 5u);

            // Assert
            run.LabelIndex.Should().Be(1);
            run.StartIndex.Should().Be(100);
            run.Length.Should().Be(50);
            run.OldId.Should().Be(0u);
            run.NewId.Should().Be(5u);
        }

        // ── InstanceDelta 프로퍼티 ───────────────────────────────────

        [Fact]
        public void InstanceDelta_WhenConstructed_ShouldHaveCorrectProperties()
        {
            // Arrange
            var before = new List<MaskLayerSnapshot.InstanceRecord>
            {
                new MaskLayerSnapshot.InstanceRecord(1u, 0, new System.Windows.Rect(0, 0, 10, 10), 100, true)
            };
            var after = new List<MaskLayerSnapshot.InstanceRecord>();

            // Act
            var delta = new MaskLayerDiff.InstanceDelta(before, after, nextIdBefore: 2u, nextIdAfter: 1u);

            // Assert
            delta.Before.Should().BeSameAs(before);
            delta.After.Should().BeSameAs(after);
            delta.NextIdBefore.Should().Be(2u);
            delta.NextIdAfter.Should().Be(1u);
        }

        // ── HasPixelChanges ──────────────────────────────────────────

        [Fact]
        public void HasPixelChanges_WhenEntriesEmpty_ShouldReturnFalse()
        {
            // Arrange
            var diff = new MaskLayerDiff(
                new List<MaskLayerDiff.PixelEntry>(),
                EmptyInstanceDelta(),
                0, 0, 0, 0);

            // Act / Assert
            diff.HasPixelChanges.Should().BeFalse();
        }

        [Fact]
        public void HasPixelChanges_WhenEntriesHaveItems_ShouldReturnTrue()
        {
            // Arrange
            var entries = new List<MaskLayerDiff.PixelEntry>
            {
                new MaskLayerDiff.PixelEntry(0, 0, 0u, 1u)
            };
            var diff = new MaskLayerDiff(entries, EmptyInstanceDelta(), 0, 0, 0, 0);

            // Act / Assert
            diff.HasPixelChanges.Should().BeTrue();
        }

        [Fact]
        public void HasPixelChanges_WhenRunsHaveItems_ShouldReturnTrue()
        {
            // Arrange
            var runs = new List<MaskLayerDiff.PixelRun>
            {
                new MaskLayerDiff.PixelRun(0, 0, 100, 0u, 1u)
            };
            var diff = new MaskLayerDiff(runs, EmptyInstanceDelta(), 0, 0, 0, 0);

            // Act / Assert
            diff.HasPixelChanges.Should().BeTrue();
        }

        // ── Entry 기반 생성자: Runs 는 빈 배열 ──────────────────────

        [Fact]
        public void EntryConstructor_ShouldHaveEmptyRuns()
        {
            // Arrange / Act
            var diff = new MaskLayerDiff(
                new List<MaskLayerDiff.PixelEntry>(),
                EmptyInstanceDelta(), 0, 0, 0, 0);

            // Assert
            diff.Runs.Should().BeEmpty();
        }

        // ── Run 기반 생성자: Entries 는 빈 배열 ─────────────────────

        [Fact]
        public void RunsConstructor_ShouldHaveEmptyEntries()
        {
            // Arrange / Act
            var diff = new MaskLayerDiff(
                new List<MaskLayerDiff.PixelRun>(),
                EmptyInstanceDelta(), 0, 0, 0, 0);

            // Assert
            diff.Entries.Should().BeEmpty();
        }

        // ── StrokeBBox 프로퍼티 ──────────────────────────────────────

        [Fact]
        public void Constructor_ShouldPreserveStrokeBBox()
        {
            // Arrange / Act
            var diff = new MaskLayerDiff(
                new List<MaskLayerDiff.PixelEntry>(),
                EmptyInstanceDelta(),
                strokeMinX: 10, strokeMinY: 20, strokeMaxX: 50, strokeMaxY: 60);

            // Assert
            diff.StrokeMinX.Should().Be(10);
            diff.StrokeMinY.Should().Be(20);
            diff.StrokeMaxX.Should().Be(50);
            diff.StrokeMaxY.Should().Be(60);
        }
    }
}
