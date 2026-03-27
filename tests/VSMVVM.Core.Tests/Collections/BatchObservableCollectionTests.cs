using System.Collections.Specialized;
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
    }
}
