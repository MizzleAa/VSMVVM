using System.Collections.Generic;
using System.Collections.Specialized;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.Collections
{
    /// <summary>
    /// 회귀 방지: BeginBatch 스코프 내에서 AddRange/RemoveRange가 호출되거나
    /// 중첩 BeginBatch가 발생해도 깊이가 0이 되기 전에는 CollectionChanged가
    /// 발화되지 않아야 한다.
    /// </summary>
    public class BatchObservableCollectionRegressionTests
    {
        [Fact]
        public void Nested_BeginBatch_Suppresses_Until_All_Disposed()
        {
            var col = new BatchObservableCollection<int>();
            int eventCount = 0;
            col.CollectionChanged += (s, e) => eventCount++;

            using (col.BeginBatch())
            {
                col.Add(1);
                using (col.BeginBatch())
                {
                    col.Add(2);
                    col.Add(3);
                    eventCount.Should().Be(0, "내부 batch 종료 전엔 발화 금지");
                }
                eventCount.Should().Be(0, "외부 batch가 아직 살아있으므로 발화 금지");
                col.Add(4);
            }

            eventCount.Should().Be(1, "최외곽 batch 종료 시 정확히 1회 발화");
            col.Should().Equal(1, 2, 3, 4);
        }

        [Fact]
        public void AddRange_Inside_BeginBatch_Does_Not_Fire_Early()
        {
            var col = new BatchObservableCollection<int>();
            int eventCount = 0;
            col.CollectionChanged += (s, e) => eventCount++;

            using (col.BeginBatch())
            {
                col.AddRange(new[] { 1, 2, 3 });
                eventCount.Should().Be(0, "AddRange가 외부 BeginBatch 스코프를 깨면 안 됨");
                col.Add(4);
                eventCount.Should().Be(0, "AddRange 종료 후에도 외부 batch가 살아있어야 함");
            }

            eventCount.Should().Be(1);
            col.Should().Equal(1, 2, 3, 4);
        }

        [Fact]
        public void AddRange_Standalone_Fires_Single_Reset_Event()
        {
            var col = new BatchObservableCollection<int>();
            var actions = new List<NotifyCollectionChangedAction>();
            col.CollectionChanged += (s, e) => actions.Add(e.Action);

            col.AddRange(new[] { 1, 2, 3, 4, 5 });

            actions.Should().ContainSingle().Which.Should().Be(NotifyCollectionChangedAction.Reset);
            col.Should().HaveCount(5);
        }

        [Fact]
        public void RemoveRange_Inside_BeginBatch_Does_Not_Fire_Early()
        {
            var col = new BatchObservableCollection<int> { 1, 2, 3, 4, 5 };
            int eventCount = 0;
            col.CollectionChanged += (s, e) => eventCount++;

            using (col.BeginBatch())
            {
                col.RemoveRange(new[] { 2, 4 });
                eventCount.Should().Be(0);
            }

            eventCount.Should().Be(1);
            col.Should().Equal(1, 3, 5);
        }

        [Fact]
        public void Empty_AddRange_Does_Not_Fire()
        {
            var col = new BatchObservableCollection<int>();
            int eventCount = 0;
            col.CollectionChanged += (s, e) => eventCount++;

            col.AddRange(new int[0]);

            eventCount.Should().Be(0, "변경 없으면 발화도 없어야 함");
        }
    }
}
