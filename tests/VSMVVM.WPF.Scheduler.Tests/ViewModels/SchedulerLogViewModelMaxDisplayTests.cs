using System;
using System.Collections.Generic;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests.ViewModels
{
    public class SchedulerLogViewModelMaxDisplayTests
    {
        private sealed class FakeSink : ISchedulerLogSink
        {
            private readonly List<SchedulerLogEntry> _all = new();
            public void Write(SchedulerLogEntry entry) { _all.Add(entry); EntryWritten?.Invoke(this, entry); }
            public IReadOnlyList<SchedulerLogEntry> GetAll() => _all;
            public void Clear() => _all.Clear();
            public event EventHandler<SchedulerLogEntry> EntryWritten;
        }

        private static SchedulerLogEntry Entry(int i) => new SchedulerLogEntry(
            DateTimeOffset.UtcNow, SchedulerLogLevel.Info,
            Guid.Empty, null, string.Empty, $"msg {i}", null);

        [StaFact]
        public void DefaultMaxDisplay_Is100()
        {
            var vm = new SchedulerLogViewModel(new FakeSink());
            vm.MaxDisplay.Should().Be(100);
        }

        [StaFact]
        public void Append_BeyondMaxDisplay_TrimsOldestFirst()
        {
            var sink = new FakeSink();
            var vm = new SchedulerLogViewModel(sink) { MaxDisplay = 3 };

            sink.Write(Entry(1));
            sink.Write(Entry(2));
            sink.Write(Entry(3));
            sink.Write(Entry(4));

            vm.Entries.Should().HaveCount(3);
            vm.Entries[0].Message.Should().Be("msg 2");
            vm.Entries[2].Message.Should().Be("msg 4");
        }
    }
}
