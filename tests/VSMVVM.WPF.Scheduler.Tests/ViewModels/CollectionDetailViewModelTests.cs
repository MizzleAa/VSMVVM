using System.Collections.Generic;
using FluentAssertions;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests.ViewModels
{
    public class CollectionDetailViewModelTests
    {
        [Fact]
        public void Construction_RootIsFlatList_ShowsRowsAndSingleBreadcrumb()
        {
            var vm = new CollectionDetailViewModel("Nums", new List<double> { 1.0, 2.5, 3.5 });

            vm.Rows.Should().HaveCount(3);
            vm.Rows[0].Value.Should().Be("1");
            vm.Rows[0].HasChildren.Should().BeFalse();
            vm.Path.Should().HaveCount(1);
            vm.Path[0].Label.Should().Be("Nums");
            vm.HeaderCount.Should().Be(3);
        }

        [Fact]
        public void DrillDown_IntoNestedList_PushesSegmentAndShowsInnerRows()
        {
            var matrix = new List<List<int>>
            {
                new() { 10, 20, 30 },
                new() { 40, 50 },
            };
            var vm = new CollectionDetailViewModel("Matrix", matrix);

            vm.Rows[0].HasChildren.Should().BeTrue();
            vm.DrillDownCommand.Execute(vm.Rows[0]);

            vm.Path.Should().HaveCount(2);
            vm.Path[1].Label.Should().Be("[0]");
            vm.Rows.Should().HaveCount(3);
            vm.Rows[0].Value.Should().Be("10");
            vm.HeaderCount.Should().Be(3);
        }

        [Fact]
        public void DrillDown_NonCollectionRow_IsIgnored()
        {
            var vm = new CollectionDetailViewModel("Nums", new List<int> { 1, 2, 3 });
            var pathCountBefore = vm.Path.Count;

            vm.DrillDownCommand.Execute(vm.Rows[0]);

            vm.Path.Count.Should().Be(pathCountBefore);
        }

        [Fact]
        public void NavigateTo_MiddleSegment_PopsDeeperSegments()
        {
            var cube = new List<List<List<int>>>
            {
                new() { new() { 1, 2 }, new() { 3, 4 } },
            };
            var vm = new CollectionDetailViewModel("Cube", cube);
            vm.DrillDownCommand.Execute(vm.Rows[0]); // → [0]
            vm.DrillDownCommand.Execute(vm.Rows[0]); // → [0][0]
            vm.Path.Should().HaveCount(3);

            vm.NavigateToCommand.Execute(vm.Path[0]);

            vm.Path.Should().HaveCount(1);
            vm.Rows.Should().HaveCount(1);
            vm.Rows[0].HasChildren.Should().BeTrue();
        }

        [Fact]
        public void DrillDown_IntoDictionaryValue_ShowsInnerListRows()
        {
            var d = new Dictionary<string, List<int>>
            {
                { "a", new() { 1, 2, 3 } },
            };
            var vm = new CollectionDetailViewModel("Scores", d);
            vm.Rows[0].Key.Should().Be("\"a\"");
            vm.Rows[0].HasChildren.Should().BeTrue();

            vm.DrillDownCommand.Execute(vm.Rows[0]);

            vm.Path[1].Label.Should().Be("[\"a\"]");
            vm.Rows.Should().HaveCount(3);
            vm.Rows[2].Value.Should().Be("3");
        }
    }
}
