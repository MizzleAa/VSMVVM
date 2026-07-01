using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.WPF.Extensions;
using Xunit;

namespace VSMVVM.WPF.Tests.Extensions
{
    // Dispatcher.Yield 를 사용하므로 WPF 메시지 펌프가 필요 — [WpfFact] 사용.
    public class ObservableCollectionExtensionsTests
    {
        // ── 인수 유효성 ────────────────────────────────────────────────

        [WpfFact]
        public async Task ReplaceWithAsync_WhenNullCollection_ShouldThrowArgumentNullException()
        {
            // Arrange
            ObservableCollection<int> collection = null;

            // Act
            Func<Task> act = () => collection.ReplaceWithAsync(new[] { 1, 2, 3 });

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("collection");
        }

        [WpfFact]
        public async Task ReplaceWithAsync_WhenNullItems_ShouldThrowArgumentNullException()
        {
            // Arrange
            var collection = new ObservableCollection<int>();

            // Act
            Func<Task> act = () => collection.ReplaceWithAsync(null);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("items");
        }

        // ── 교체 동작 ──────────────────────────────────────────────────

        [WpfFact]
        public async Task ReplaceWithAsync_WhenCalled_ShouldClearExistingItemsAndAddNew()
        {
            // Arrange
            var collection = new ObservableCollection<int> { 1, 2, 3 };
            var newItems = new[] { 10, 20, 30, 40 };

            // Act
            await collection.ReplaceWithAsync(newItems, batchSize: 2)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            collection.Should().BeEquivalentTo(newItems, o => o.WithStrictOrdering());
        }

        [WpfFact]
        public async Task ReplaceWithAsync_WhenReplacingWithEmpty_ShouldResultInEmptyCollection()
        {
            // Arrange
            var collection = new ObservableCollection<int> { 1, 2, 3 };

            // Act
            await collection.ReplaceWithAsync(Array.Empty<int>())
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            collection.Should().BeEmpty();
        }

        [WpfFact]
        public async Task ReplaceWithAsync_WhenBatchSizeZero_ShouldAddAllItemsWithoutYielding()
        {
            // Arrange
            var collection = new ObservableCollection<string>();
            var items = new[] { "a", "b", "c", "d", "e" };

            // Act — batchSize=0 은 yield 없이 한 번에 추가
            await collection.ReplaceWithAsync(items, batchSize: 0)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            collection.Should().BeEquivalentTo(items, o => o.WithStrictOrdering());
        }

        [WpfFact]
        public async Task ReplaceWithAsync_WhenLargeBatch_ShouldPreserveAllItems()
        {
            // Arrange
            var collection = new ObservableCollection<int>();
            var items = new List<int>();
            for (int i = 0; i < 100; i++) items.Add(i);

            // Act
            await collection.ReplaceWithAsync(items, batchSize: 10)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            collection.Should().HaveCount(100);
            collection.Should().BeEquivalentTo(items, o => o.WithStrictOrdering());
        }

        // ── CollectionChanged 이벤트 순서 ─────────────────────────────

        [WpfFact]
        public async Task ReplaceWithAsync_ShouldFireClearEventFirst()
        {
            // Arrange
            var collection = new ObservableCollection<int> { 1, 2, 3 };
            var actions = new List<NotifyCollectionChangedAction>();
            collection.CollectionChanged += (_, e) => actions.Add(e.Action);

            // Act
            await collection.ReplaceWithAsync(new[] { 10, 20 }, batchSize: 5)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Assert — 첫 이벤트가 Reset(=Clear)이어야 한다
            actions.Should().NotBeEmpty();
            actions[0].Should().Be(NotifyCollectionChangedAction.Reset, "Clear 가 먼저 발생해야 한다");
        }

        [WpfFact]
        public async Task ReplaceWithAsync_WhenItemsExistBefore_ShouldContainOnlyNewItemsAfter()
        {
            // Arrange — 이전 아이템이 남지 않는지 확인
            var collection = new ObservableCollection<string> { "old1", "old2", "old3" };

            // Act
            await collection.ReplaceWithAsync(new[] { "new1" }, batchSize: 5)
                .WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            collection.Should().NotContain("old1");
            collection.Should().NotContain("old2");
            collection.Should().NotContain("old3");
            collection.Should().Contain("new1");
        }
    }
}
