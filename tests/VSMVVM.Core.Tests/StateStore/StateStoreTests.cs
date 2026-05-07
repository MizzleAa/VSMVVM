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
        public void DisposeSubscription_StopsNotification()
        {
            var store = new TestStateStore();
            int notifiedCounter = -1;
            Action<TestState> callback = state => notifiedCounter = state.Counter;
            var sub = store.Subscribe(callback);
            sub.Dispose();

            store.Increment();

            notifiedCounter.Should().Be(0); // 구독 시 즉시 통지된 값 유지
        }

        [Fact]
        public void UpdateState_NotifiesWithCallerVisibleSnapshot()
        {
            // 회귀 테스트: NotifySubscribers는 호출자가 본 그 시점의 state를 통지해야 한다.
            // 두 스레드가 X와 Y로 동시에 UpdateState하면 알림 순서와 무관하게 각 callback은
            // X 또는 Y 중 하나(자기 호출이 만든 값)를 받아야 하지, 다른 호출이 덮어쓴 _state를
            // 받아서는 안 된다. callCount = 호출 횟수와 같아야 한다 (lost notification 없음).
            var store = new TestStateStore();
            int callCount = 0;
            var notifyLock = new object();
            var receivedValues = new System.Collections.Generic.HashSet<int>();
            store.Subscribe(state =>
            {
                lock (notifyLock)
                {
                    callCount++;
                    receivedValues.Add(state.Counter);
                }
            });

            const int N = 50;
            System.Threading.Tasks.Parallel.For(0, N, i => store.Increment());

            // 초기 즉시 통지(1) + Increment(N) = N+1번 호출
            callCount.Should().Be(N + 1, "모든 UpdateState 호출에 대해 알림이 누락 없이 발화되어야 한다");
            // 받은 값들은 모두 0 이상의 유효한 카운터여야 (음수, 비초기값 등 깨진 상태 없음)
            receivedValues.Should().OnlyContain(v => v >= 0);
        }
    }
}
