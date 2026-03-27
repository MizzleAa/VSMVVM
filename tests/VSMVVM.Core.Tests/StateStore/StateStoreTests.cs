using System;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.StateStore
{
    public class TestState
    {
        public int Counter { get; set; }
    }

    public class TestStateStore : StateStoreBase<TestState>
    {
        public TestStateStore() : base(new TestState { Counter = 0 }) { }

        public void Increment()
        {
            UpdateState(new TestState { Counter = State.Counter + 1 });
        }
    }

    public class StateStoreTests
    {
        [Fact]
        public void InitialState_ReturnsProvidedState()
        {
            var store = new TestStateStore();

            store.State.Counter.Should().Be(0);
        }

        [Fact]
        public void UpdateState_NotifiesSubscribers()
        {
            var store = new TestStateStore();
            int notifiedCounter = -1;
            store.Subscribe(state => notifiedCounter = state.Counter);

            store.Increment();

            notifiedCounter.Should().Be(1);
        }

        [Fact]
        public void Subscribe_ImmediatelyNotifiesCurrentState()
        {
            var store = new TestStateStore();
            int notifiedCounter = -1;

            store.Subscribe(state => notifiedCounter = state.Counter);

            notifiedCounter.Should().Be(0);
        }

        [Fact]
        public void Unsubscribe_StopsNotification()
        {
            var store = new TestStateStore();
            int notifiedCounter = -1;
            Action<TestState> callback = state => notifiedCounter = state.Counter;
            store.Subscribe(callback);
            store.Unsubscribe(callback);

            store.Increment();

            notifiedCounter.Should().Be(0); // 구독 시 즉시 통지된 값 유지
        }
    }
}
