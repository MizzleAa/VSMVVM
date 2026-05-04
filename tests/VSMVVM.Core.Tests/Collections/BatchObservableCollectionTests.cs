using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.Collections
{
    public class BatchObservableCollectionTests
    {
        [Fact]
        public void AddRange_FiresCollectionChangedOnce()
        {
            var collection = new BatchObservableCollection<int>();
            int eventCount = 0;
            collection.CollectionChanged += (s, e) => eventCount++;

            collection.AddRange(new[] { 1, 2, 3, 4, 5 });

            eventCount.Should().Be(1);
            collection.Count.Should().Be(5);
        }

        [Fact]
        public void RemoveRange_FiresCollectionChangedOnce()
        {
            var collection = new BatchObservableCollection<int>(new[] { 1, 2, 3, 4, 5 });
            int eventCount = 0;
            collection.CollectionChanged += (s, e) => eventCount++;

            collection.RemoveRange(new[] { 2, 4 });

            eventCount.Should().Be(1);
            collection.Count.Should().Be(3);
        }

        [Fact]
        public void AddRange_FiresCountAndIndexerPropertyChanged()
        {
            // 회귀 테스트: 배치 모드에서도 Count/Item[] PropertyChanged가 발화되어야
            // 직접 바인딩한 UI(예: <TextBlock Text="{Binding Items.Count}"/>)가 갱신된다.
            var collection = new BatchObservableCollection<int>();
            var changedProperties = new List<string>();
            ((INotifyPropertyChanged)collection).PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName);

            collection.AddRange(new[] { 1, 2, 3 });

            changedProperties.Should().Contain(nameof(collection.Count));
            changedProperties.Should().Contain("Item[]");
        }

        [Fact]
        public void RemoveRange_FiresCountAndIndexerPropertyChanged()
        {
            var collection = new BatchObservableCollection<int>(new[] { 1, 2, 3, 4, 5 });
            var changedProperties = new List<string>();
            ((INotifyPropertyChanged)collection).PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName);

            collection.RemoveRange(new[] { 2, 4 });

            changedProperties.Should().Contain(nameof(collection.Count));
            changedProperties.Should().Contain("Item[]");
        }

        [Fact]
        public void BeginBatchWithRegularAdd_DoesNotLeakPropertyChangedDuringBatch()
        {
            // 회귀 테스트: BeginBatch 안에서 일반 Add를 호출하면 부모 ObservableCollection의
            // OnPropertyChanged가 누설되어 N+1번 발화되던 버그.
            var collection = new BatchObservableCollection<int>();
            var changedProperties = new List<string>();
            ((INotifyPropertyChanged)collection).PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName);
            int collectionChangedCount = 0;
            collection.CollectionChanged += (s, e) => collectionChangedCount++;

            using (collection.BeginBatch())
            {
                collection.Add(1);
                collection.Add(2);
                collection.Add(3);
                changedProperties.Should().BeEmpty("배치 중에는 PropertyChanged도 묻혀야 한다");
                collectionChangedCount.Should().Be(0, "배치 중에는 CollectionChanged도 묻혀야 한다");
            }

            collectionChangedCount.Should().Be(1, "배치 종료 시 CollectionChanged는 정확히 한 번만 발화");
            changedProperties.Should().ContainSingle(p => p == nameof(collection.Count));
            changedProperties.Should().ContainSingle(p => p == "Item[]");
        }
    }
}
