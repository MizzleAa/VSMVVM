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
    }
}
