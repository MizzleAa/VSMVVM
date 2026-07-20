using System.Collections.Generic;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests.ViewModels
{
    public class PinValueSnapshotTests
    {
        private static PinValueSnapshot Snap(object value)
            => new PinValueSnapshot("Out", "Out", value, PinDirection.Output);

        [Fact]
        public void DisplayValue_Null_ShowsPlaceholder()
        {
            var s = Snap(null);
            s.DisplayValue.Should().Be("(null)");
            s.IsCollection.Should().BeFalse();
            s.ExpandedRows.Should().BeEmpty();
        }

        [Fact]
        public void DisplayValue_String_IsQuotedAndNotACollection()
        {
            var s = Snap("hello");
            s.DisplayValue.Should().Be("\"hello\"");
            s.IsCollection.Should().BeFalse();
            s.ExpandedRows.Should().BeEmpty();
        }

        [Fact]
        public void DisplayValue_Double_UsesInvariantCulture()
        {
            Snap(1.5).DisplayValue.Should().Be("1.5");
        }

        [Fact]
        public void DisplayValue_ListOfDouble_SummarizesAllElementsWhenUnderPreview()
        {
            var s = Snap(new List<double> { 1.0, 2.5, 3.5, 4.0 });
            s.IsCollection.Should().BeTrue();
            s.DisplayValue.Should().Be("[1, 2.5, 3.5, 4] (4)");
        }

        [Fact]
        public void ExpandedRows_ListOfDouble_UsesIndexAsKey()
        {
            var s = Snap(new List<double> { 1.0, 2.5, 3.5, 4.0 });
            s.ExpandedRows.Should().HaveCount(4);
            s.ExpandedRows[0].Key.Should().Be("0");
            s.ExpandedRows[0].Value.Should().Be("1");
            s.ExpandedRows[1].Value.Should().Be("2.5");
            s.ExpandedRows[3].Key.Should().Be("3");
            s.ExpandedRows[3].Value.Should().Be("4");
        }

        [Fact]
        public void DisplayValue_LargeList_TruncatesPreviewButKeepsTotalCount()
        {
            var list = new List<int> { 10, 20, 30, 40, 50, 60, 70 };
            var s = Snap(list);
            s.DisplayValue.Should().Be("[10, 20, 30, 40, 50, ...] (7)");
            s.ExpandedRows.Should().HaveCount(7);
        }

        [Fact]
        public void DisplayValue_Array_TreatedAsEnumerable()
        {
            var s = Snap(new[] { "a", "b" });
            s.IsCollection.Should().BeTrue();
            s.DisplayValue.Should().Be("[\"a\", \"b\"] (2)");
            s.ExpandedRows.Should().HaveCount(2);
            s.ExpandedRows[0].Value.Should().Be("\"a\"");
        }

        [Fact]
        public void DisplayValue_Dictionary_ShowsKeyValuePairsWithCount()
        {
            var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
            var s = Snap(dict);
            s.IsCollection.Should().BeTrue();
            s.DisplayValue.Should().Contain("\"a\": 1").And.Contain("\"b\": 2").And.EndWith("(2)");
            s.ExpandedRows.Should().HaveCount(2);
            s.ExpandedRows[0].Key.Should().Be("\"a\"");
            s.ExpandedRows[0].Value.Should().Be("1");
        }

        [Fact]
        public void DisplayValue_LongString_IsTruncatedToCellWidth()
        {
            var s = Snap(new string('x', 200));
            s.DisplayValue.Length.Should().Be(100);
            s.DisplayValue.Should().EndWith("...");
        }

        [Fact]
        public void ExpandedRows_IsCachedAcrossCalls()
        {
            var s = Snap(new List<int> { 1, 2, 3 });
            var first = s.ExpandedRows;
            var second = s.ExpandedRows;
            second.Should().BeSameAs(first);
        }

        [Fact]
        public void DisplayValue_NestedList_ShowsInnerSummaries()
        {
            var matrix = new List<List<int>>
            {
                new() { 1, 2, 3 },
                new() { 4, 5, 6 },
            };
            var s = Snap(matrix);
            s.IsCollection.Should().BeTrue();
            s.DisplayValue.Should().Contain("[1, 2, 3] (3)").And.Contain("[4, 5, 6] (3)").And.EndWith("(2)");
        }

        [Fact]
        public void ExpandedRows_NestedList_InnerCollectionIsSummarized()
        {
            var matrix = new List<List<int>>
            {
                new() { 1, 2, 3 },
                new() { 4, 5, 6 },
            };
            var s = Snap(matrix);
            s.ExpandedRows.Should().HaveCount(2);
            s.ExpandedRows[0].Key.Should().Be("0");
            s.ExpandedRows[0].Value.Should().Be("[1, 2, 3] (3)");
            s.ExpandedRows[1].Value.Should().Be("[4, 5, 6] (3)");
        }

        [Fact]
        public void DisplayValue_3DList_TruncatesAtSummaryLimit()
        {
            var cube = new List<List<List<double>>>
            {
                new() { new() { 1.0, 1.1 }, new() { 1.2, 1.3 } },
                new() { new() { 2.0, 2.1 }, new() { 2.2, 2.3 } },
            };
            var s = Snap(cube);
            s.IsCollection.Should().BeTrue();
            s.ExpandedRows.Should().HaveCount(2);
            s.ExpandedRows[0].Value.Should().StartWith("[[1, 1.1] (2), [1.2, 1.3] (2)] (2)");
        }

        [Fact]
        public void DisplayValue_DictionaryOfList_ShowsNestedElements()
        {
            var d = new Dictionary<string, List<int>>
            {
                { "a", new() { 1, 2, 3 } },
                { "b", new() { 4, 5 } },
            };
            var s = Snap(d);
            s.IsCollection.Should().BeTrue();
            s.DisplayValue.Should().Contain("\"a\": [1, 2, 3] (3)").And.Contain("\"b\": [4, 5] (2)");
        }
    }
}
