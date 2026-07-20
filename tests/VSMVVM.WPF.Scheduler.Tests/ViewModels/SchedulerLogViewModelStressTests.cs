using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests.ViewModels
{
    /// <summary>
    /// SchedulerLogViewModel 부하/응답성 테스트.
    /// 실제 앱에서 EntryWritten 이 폭주할 때 UI 스레드가 굳는 문제 재현/방어용.
    ///
    /// 재현 전제: 다른 스레드에서 sink.Write 를 수천 회 호출하면 VM 은 각 이벤트마다
    /// Dispatcher.BeginInvoke 를 큐잉한다. Dispatcher 처리 지연 + Entries trim(RemoveAt(0)) 이
    /// 응답성을 죽이는지, 그리고 최종 상태는 정상인지.
    /// </summary>
    [Trait("Category", "Stress")]
    public class SchedulerLogViewModelStressTests
    {
        private sealed class FakeSink : ISchedulerLogSink
        {
            private readonly List<SchedulerLogEntry> _all = new();
            public void Write(SchedulerLogEntry entry)
            {
                lock (_all) _all.Add(entry);
                EntryWritten?.Invoke(this, entry);
            }
            public IReadOnlyList<SchedulerLogEntry> GetAll() { lock (_all) return _all.ToArray(); }
            public void Clear() { lock (_all) _all.Clear(); }
            public event EventHandler<SchedulerLogEntry> EntryWritten;
        }

        private static SchedulerLogEntry Entry(int i) => new SchedulerLogEntry(
            DateTimeOffset.UtcNow, SchedulerLogLevel.Info,
            Guid.Empty, null, string.Empty, $"msg {i}", null);

        /// <summary>
        /// STA 스레드 + Dispatcher.Run 상황에서 background thread 가 sink.Write 를 폭주시키는 시나리오를 구성.
        /// action 실행이 끝나면 Dispatcher 를 shutdown 하고 예외를 밖으로 전파.
        /// </summary>
        private static void RunOnDispatcher(Action<Dispatcher> action, int dispatcherShutdownTimeoutMs = 5000)
        {
            Exception captured = null;
            var ready = new ManualResetEventSlim(false);
            Dispatcher dispatcher = null;

            var t = new Thread(() =>
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            ready.Wait();

            try
            {
                action(dispatcher);
            }
            catch (Exception ex)
            {
                captured = ex;
            }
            finally
            {
                dispatcher.InvokeShutdown();
                t.Join(dispatcherShutdownTimeoutMs);
            }

            if (captured != null) throw captured;
        }

        [Fact]
        public void Stress_FloodFromBackgroundThread_DoesNotHangOrCrash_AndCapsEntriesToMaxDisplay()
        {
            const int totalWrites = 5000;
            const int maxDisplay = 100;

            RunOnDispatcher(dispatcher =>
            {
                var sink = new FakeSink();
                var vm = new SchedulerLogViewModel(sink, dispatcher) { MaxDisplay = maxDisplay };

                var sw = Stopwatch.StartNew();
                Parallel.For(0, totalWrites, i => sink.Write(Entry(i)));

                // 큐에 남은 BeginInvoke 를 전부 처리할 때까지 대기.
                var drainStatus = dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle)
                                            .Wait(TimeSpan.FromSeconds(10));
                sw.Stop();

                drainStatus.Should().Be(DispatcherOperationStatus.Completed,
                    "Dispatcher 가 10초 안에 큐를 소진하지 못하면 hang 으로 간주");

                dispatcher.Invoke(() =>
                {
                    vm.Entries.Count.Should().BeLessOrEqualTo(maxDisplay);
                });
            });
        }

        [Fact]
        public void Stress_FloodFromMultipleThreads_UiResponsivenessStaysBelowThreshold()
        {
            const int totalWrites = 2000;
            const int maxDisplay = 100;
            const int responsivenessBudgetMs = 500;

            RunOnDispatcher(dispatcher =>
            {
                var sink = new FakeSink();
                var vm = new SchedulerLogViewModel(sink, dispatcher) { MaxDisplay = maxDisplay };

                Parallel.For(0, totalWrites, i => sink.Write(Entry(i)));

                var sw = Stopwatch.StartNew();
                // 새 delegate 가 Send/Normal 우선순위로 실행되기까지 걸리는 시간 = UI 응답 지연 근사치.
                var status = dispatcher.InvokeAsync(() => { }, DispatcherPriority.Normal)
                                       .Wait(TimeSpan.FromMilliseconds(responsivenessBudgetMs * 4));
                sw.Stop();

                status.Should().Be(DispatcherOperationStatus.Completed);
                sw.ElapsedMilliseconds.Should().BeLessThan(
                    responsivenessBudgetMs,
                    "폭주 중에도 UI 우선순위 delegate 응답이 이 예산을 넘으면 사용자가 hang 으로 체감함");
            });
        }

        /// <summary>
        /// 계약: N건 폭주 시 VM 이 UI 스레드에 예약하는 Dispatcher 콜백 수는 N 을 크게 밑돌아야 한다.
        /// 즉, 여러 EntryWritten 이 하나의 flush 콜백으로 coalescing 되어야 한다.
        ///
        /// WPF ItemsControl 은 매 CollectionChanged 마다 measure/arrange invalidate 를 하지만 실제 render 는
        /// dispatcher tick 종료 시 1회이므로, "하나의 tick 안에서 N건 처리" 여부가 UI 응답성의 결정 요인.
        /// Coalescing 이 없으면 매 이벤트마다 별도 BeginInvoke 가 큐잉되어 tick 이 N번 소비 → 프리즈.
        /// </summary>
        [Fact]
        public void Stress_FloodCoalescesDispatcherCallbacks()
        {
            const int totalWrites = 10_000;
            const int maxDisplay = 100;
            // 폭주 write 대비 5% 이하로 배치되어야 통과.
            const int allowedRatio = 20;

            RunOnDispatcher(dispatcher =>
            {
                var sink = new FakeSink();
                var vm = new SchedulerLogViewModel(sink, dispatcher) { MaxDisplay = maxDisplay };

                // Entries.CollectionChanged 는 반드시 UI 스레드에서만 발생 (VM 계약).
                // 각 CollectionChanged 는 하나의 dispatcher 콜백 안에서 여러 개 묶여 발생할 수 있음.
                // "콜백 배치 수" 는 CollectionChanged 사이에 UI 스레드가 다른 delegate 를 실행했는지로 근사.
                // 직접 측정: BeginInvoke 큐 소진 후 flush 콜백이 몇 번 실행됐는지를 세기 위해
                // dispatcher 콜백 카운터를 wrap 하는 방식이 어려우므로,
                // 대신 CollectionChanged 이벤트를 세되 "flush 배치의 마지막 Add 로 인한 변화" 만 세도록
                // Reset 액션이나 Add 액션의 batch grouping 을 관찰. 단순화: CollectionChanged 총합.
                //
                // 배치 flush 안에서 Entries.Add 를 여러 번 하면 CollectionChanged 는 여전히 write 수만큼 발생 → 실패.
                // → 라이브러리는 flush 안에서 Entries 를 "AddRange 시맨틱" 으로 처리해야 함 (예: Reset + 재빌드,
                //   또는 커스텀 ObservableCollection 로 SuppressNotification).
                int changes = 0;
                vm.Entries.CollectionChanged += (_, __) => Interlocked.Increment(ref changes);

                Parallel.For(0, totalWrites, i => sink.Write(Entry(i)));

                var drain = dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle)
                                      .Wait(TimeSpan.FromSeconds(30));
                drain.Should().Be(DispatcherOperationStatus.Completed);

                var maxAllowed = totalWrites / allowedRatio;
                changes.Should().BeLessThan(maxAllowed,
                    $"폭주 시 CollectionChanged 는 배치되어야 함 — 실제 {changes} 회, 상한 {maxAllowed}");
            });
        }
    }
}
