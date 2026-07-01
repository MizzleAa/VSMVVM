using System;
using System.Windows.Media;
using FluentAssertions;
using VSMVVM.WPF.Imaging;
using Xunit;

namespace VSMVVM.WPF.Tests.Imaging
{
    public class LabelClassCollectionTests
    {
        // ── NextFreeIndex ──────────────────────────────────────────────

        [Fact]
        public void NextFreeIndex_WhenEmpty_ShouldReturnOne()
        {
            // Arrange
            var col = new LabelClassCollection();

            // Act
            var idx = col.NextFreeIndex();

            // Assert
            idx.Should().Be(1);
        }

        [Fact]
        public void NextFreeIndex_WhenOneExists_ShouldSkipExistingIndex()
        {
            // Arrange
            var col = new LabelClassCollection();
            col.Add("Label A", Colors.Red); // index 1 할당

            // Act
            var idx = col.NextFreeIndex();

            // Assert
            idx.Should().Be(2, "1 은 이미 사용 중이므로 2 를 반환해야 한다");
        }

        [Fact]
        public void NextFreeIndex_WhenGapExists_ShouldReturnLowestFreeIndex()
        {
            // Arrange
            var col = new LabelClassCollection();
            col.AddWithIndex(1, "A", Colors.Red);
            col.AddWithIndex(3, "C", Colors.Blue);

            // Act — 1은 있고, 2는 없음
            var idx = col.NextFreeIndex();

            // Assert
            idx.Should().Be(2);
        }

        [Fact]
        public void NextFreeIndex_WhenAllIndicesFull_ShouldThrowInvalidOperationException()
        {
            // Arrange — 1~255 모두 채움
            var col = new LabelClassCollection();
            for (int i = 1; i <= 255; i++)
                col.Add(new LabelClass { Index = i, Name = $"L{i}", Color = Colors.Red, IsVisible = true });

            // Act
            Action act = () => col.NextFreeIndex();

            // Assert
            act.Should().Throw<InvalidOperationException>("1~255 전부 사용 중이면 예외");
        }

        // ── Add(name, color) ───────────────────────────────────────────

        [Fact]
        public void Add_WithNameAndColor_ShouldAssignNextFreeIndexAndAddToCollection()
        {
            // Arrange
            var col = new LabelClassCollection();

            // Act
            var label = col.Add("MyLabel", Colors.Green);

            // Assert
            label.Index.Should().Be(1);
            label.Name.Should().Be("MyLabel");
            label.Color.Should().Be(Colors.Green);
            label.IsVisible.Should().BeTrue();
            col.Should().HaveCount(1);
            col[0].Should().BeSameAs(label);
        }

        [Fact]
        public void Add_MultipleTimes_ShouldAssignConsecutiveIndices()
        {
            // Arrange
            var col = new LabelClassCollection();

            // Act
            var a = col.Add("A", Colors.Red);
            var b = col.Add("B", Colors.Blue);
            var c = col.Add("C", Colors.Green);

            // Assert
            a.Index.Should().Be(1);
            b.Index.Should().Be(2);
            c.Index.Should().Be(3);
        }

        // ── AddWithIndex ──────────────────────────────────────────────

        [Fact]
        public void AddWithIndex_WhenValidIndex_ShouldAddAtSpecifiedIndex()
        {
            // Arrange
            var col = new LabelClassCollection();

            // Act
            var label = col.AddWithIndex(5, "Five", Colors.Yellow);

            // Assert
            label.Index.Should().Be(5);
            col.GetByIndex(5).Should().NotBeNull();
        }

        [Fact]
        public void AddWithIndex_WhenIndexIsZero_ShouldThrowArgumentOutOfRangeException()
        {
            // Arrange
            var col = new LabelClassCollection();

            // Act
            Action act = () => col.AddWithIndex(0, "bg", Colors.Black);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>("0 은 배경 예약 인덱스이므로 직접 추가 불가");
        }

        [Fact]
        public void AddWithIndex_WhenIndexGreaterThan255_ShouldThrowArgumentOutOfRangeException()
        {
            // Arrange
            var col = new LabelClassCollection();

            // Act
            Action act = () => col.AddWithIndex(256, "Too big", Colors.Red);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void AddWithIndex_WhenDuplicateIndex_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var col = new LabelClassCollection();
            col.AddWithIndex(3, "First", Colors.Red);

            // Act
            Action act = () => col.AddWithIndex(3, "Second", Colors.Blue);

            // Assert
            act.Should().Throw<InvalidOperationException>("같은 인덱스 중복 추가는 불가");
        }

        // ── GetByIndex ────────────────────────────────────────────────

        [Fact]
        public void GetByIndex_WhenIndexExists_ShouldReturnLabel()
        {
            // Arrange
            var col = new LabelClassCollection();
            col.AddWithIndex(7, "Seven", Colors.Pink);

            // Act
            var label = col.GetByIndex(7);

            // Assert
            label.Should().NotBeNull();
            label!.Index.Should().Be(7);
            label.Name.Should().Be("Seven");
        }

        [Fact]
        public void GetByIndex_WhenIndexNotExists_ShouldReturnNull()
        {
            // Arrange
            var col = new LabelClassCollection();

            // Act
            var label = col.GetByIndex(99);

            // Assert
            label.Should().BeNull();
        }

        // ── RemoveItem — 배경 라벨 보호 ────────────────────────────────

        [Fact]
        public void RemoveItem_WhenItemIsBackground_ShouldNotRemove()
        {
            // Arrange — 인덱스 0 라벨을 컬렉션 앞에 추가
            var col = new LabelClassCollection();
            var bgLabel = new LabelClass { Index = LabelClassCollection.BackgroundIndex, Name = "Background", Color = Colors.Black, IsVisible = true };
            col.Add(bgLabel); // ObservableCollection.Add 직접 호출

            // Act
            col.RemoveAt(0); // RemoveItem(0) 호출 — this[0].Index == 0 이므로 거부

            // Assert
            col.Should().HaveCount(1, "배경 라벨은 제거할 수 없어야 한다");
        }

        [Fact]
        public void RemoveItem_WhenItemIsNonBackground_ShouldRemove()
        {
            // Arrange
            var col = new LabelClassCollection();
            col.Add("Label1", Colors.Red); // index 1
            col.Should().HaveCount(1);

            // Act
            col.RemoveAt(0);

            // Assert
            col.Should().HaveCount(0, "배경이 아닌 라벨은 정상 제거");
        }

        // ── SetItem — 배경 라벨 덮어쓰기 방지 ───────────────────────

        [Fact]
        public void SetItem_WhenTargetIsBackground_ShouldNotReplace()
        {
            // Arrange
            var col = new LabelClassCollection();
            var bgLabel = new LabelClass { Index = LabelClassCollection.BackgroundIndex, Name = "Background", Color = Colors.Black, IsVisible = true };
            col.Add(bgLabel);

            var newLabel = new LabelClass { Index = 99, Name = "Replacement", Color = Colors.Red, IsVisible = true };

            // Act
            col[0] = newLabel; // SetItem 호출

            // Assert
            col[0].Should().BeSameAs(bgLabel, "배경 라벨 위치는 덮어쓰기 불가");
        }

        // ── CollectionChanged 이벤트 ──────────────────────────────────

        [Fact]
        public void Add_ShouldRaiseCollectionChangedEvent()
        {
            // Arrange
            var col = new LabelClassCollection();
            int eventCount = 0;
            col.CollectionChanged += (_, _) => eventCount++;

            // Act
            col.Add("Test", Colors.Red);

            // Assert
            eventCount.Should().Be(1);
        }

        // ── 상수 확인 ─────────────────────────────────────────────────

        [Fact]
        public void Constants_ShouldHaveCorrectValues()
        {
            LabelClassCollection.BackgroundIndex.Should().Be(0);
            LabelClassCollection.MaxIndex.Should().Be(255);
        }
    }
}
