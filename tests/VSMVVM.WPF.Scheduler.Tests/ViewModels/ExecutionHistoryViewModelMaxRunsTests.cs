using System;
using System.Collections.Generic;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests.ViewModels
{
    public class ExecutionHistoryViewModelMaxRunsTests
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

        [StaFact]
        public void DefaultMaxRuns_Is10()
        {
            var vm = new ExecutionHistoryViewModel(new FakeStore());
            vm.MaxRuns.Should().Be(10);
        }

        [StaFact]
        public void RunAdded_BeyondMaxRuns_TrimsOldestFirst()
        {
            var store = new FakeStore();
            var vm = new ExecutionHistoryViewModel(store) { MaxRuns = 3 };
            var r1 = Run(); var r2 = Run(); var r3 = Run(); var r4 = Run();

            store.Add(r1); store.Add(r2); store.Add(r3); store.Add(r4);

            vm.Runs.Should().HaveCount(3);
            vm.Runs[0].Should().BeSameAs(r2);
            vm.Runs[2].Should().BeSameAs(r4);
            vm.SelectedRun.Should().BeSameAs(r4);
        }

        [StaFact]
        public void ConstructionWithPreloadedStore_TrimsToMaxRuns()
        {
            var store = new FakeStore();
            for (int i = 0; i < 200; i++) store.Preload.Add(Run());

            var vm = new ExecutionHistoryViewModel(store);

            vm.Runs.Should().HaveCount(10);
        }

        [StaFact]
        public void MaxRunsZeroOrNegative_MeansUnlimited()
        {
            var store = new FakeStore();
            var vm = new ExecutionHistoryViewModel(store) { MaxRuns = 0 };

            for (int i = 0; i < 150; i++) store.Add(Run());

            vm.Runs.Should().HaveCount(150);
        }
    }
}
