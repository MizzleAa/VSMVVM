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
    /// ExecutionHistoryViewModel 부하 테스트. Runs 폭주 시 hang/크래시 방지 + Runs 상한 유지.
    /// </summary>
    public class ExecutionHistoryViewModelStressTests
    {
        private sealed class FakeStore : IExecutionHistoryStore
        {
            public List<ExecutionRun> Preload { get; } = new();
            public void Add(ExecutionRun run) => RunAdded?.Invoke(this, run);
            public IReadOnlyList<ExecutionRun> GetAll() => Preload;
            public void Clear() => Preload.Clear();
            public event EventHandler<ExecutionRun> RunAdded;
        }

        private static ExecutionRun Run() => new ExecutionRun(Guid.NewGuid(), Guid.Empty, DateTimeOffset.UtcNow);

        private static void RunOnDispatcher(Action<Dispatcher> action, int timeoutMs = 5000)
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

            try { action(dispatcher); }
            catch (Exception ex) { captured = ex; }
            finally
            {
                dispatcher.InvokeShutdown();
                t.Join(timeoutMs);
            }

            if (captured != null) throw captured;
        }

        [Fact]
        public void Stress_FloodRunsFromBackgroundThread_DoesNotHang_AndCapsToMaxRuns()
        {
            const int totalAdds = 5000;
            const int maxRuns = 100;

            RunOnDispatcher(dispatcher =>
            {
                var store = new FakeStore();
                var vm = new ExecutionHistoryViewModel(store, dispatcher) { MaxRuns = maxRuns };

                Parallel.For(0, totalAdds, _ => store.Add(Run()));

                var drainStatus = dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle)
                                            .Wait(TimeSpan.FromSeconds(10));
                drainStatus.Should().Be(DispatcherOperationStatus.Completed);

                dispatcher.Invoke(() =>
                {
                    vm.Runs.Count.Should().BeLessOrEqualTo(maxRuns);
                });
            });
        }

        /// <summary>
        /// 계약: Runs 폭주 시 CollectionChanged 는 배치되어야 한다 (WPF ItemsControl hang 방지).
        /// </summary>
        [Fact]
        public void Stress_FloodCoalescesCollectionChangedEvents()
        {
            const int totalAdds = 10_000;
            const int maxRuns = 100;
            const int allowedRatio = 20;

            RunOnDispatcher(dispatcher =>
            {
                var store = new FakeStore();
                var vm = new ExecutionHistoryViewModel(store, dispatcher) { MaxRuns = maxRuns };

                int changes = 0;
                vm.Runs.CollectionChanged += (_, __) => Interlocked.Increment(ref changes);

                Parallel.For(0, totalAdds, _ => store.Add(Run()));

                var drain = dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle)
                                      .Wait(TimeSpan.FromSeconds(30));
                drain.Should().Be(DispatcherOperationStatus.Completed);

                var maxAllowed = totalAdds / allowedRatio;
                changes.Should().BeLessThan(maxAllowed,
                    $"폭주 시 CollectionChanged 는 배치되어야 함 — 실제 {changes} 회, 상한 {maxAllowed}");
            });
        }

        [Fact]
        public void Stress_FloodRunsFromMultipleThreads_UiResponsivenessStaysBelowThreshold()
        {
            const int totalAdds = 2000;
            const int maxRuns = 100;
            const int responsivenessBudgetMs = 500;

            RunOnDispatcher(dispatcher =>
            {
                var store = new FakeStore();
                var vm = new ExecutionHistoryViewModel(store, dispatcher) { MaxRuns = maxRuns };

                Parallel.For(0, totalAdds, _ => store.Add(Run()));

                var sw = Stopwatch.StartNew();
                var status = dispatcher.InvokeAsync(() => { }, DispatcherPriority.Normal)
                                       .Wait(TimeSpan.FromMilliseconds(responsivenessBudgetMs * 4));
                sw.Stop();

                status.Should().Be(DispatcherOperationStatus.Completed);
                sw.ElapsedMilliseconds.Should().BeLessThan(responsivenessBudgetMs);
            });
        }
    }
}
