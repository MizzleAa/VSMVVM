using System;
using System.ComponentModel;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.MVVM
{
    public class TestViewModel : ViewModelBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
    }

    public class ViewModelBaseTests
    {
        [Fact]
        public void SetProperty_RaisesPropertyChanged()
        {
            var vm = new TestViewModel();
            string changedProperty = null;
            vm.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            vm.Name = "Hello";

            changedProperty.Should().Be(nameof(TestViewModel.Name));
        }

        [Fact]
        public void SetProperty_SameValue_DoesNotRaisePropertyChanged()
        {
            var vm = new TestViewModel { Name = "Hello" };
            bool raised = false;
            vm.PropertyChanged += (s, e) => raised = true;

            vm.Name = "Hello";

            raised.Should().BeFalse();
        }

        [Fact]
        public void SetProperty_ReturnsTrue_WhenValueChanged()
        {
            var vm = new TestViewModel();

            vm.Name = "Test";

            vm.Name.Should().Be("Test");
        }
    }

    public class RelayCommandTests
    {
        [Fact]
        public void Execute_InvokesAction()
        {
            bool executed = false;
            var cmd = new RelayCommand(() => executed = true);

            cmd.Execute(null);

            executed.Should().BeTrue();
        }

        [Fact]
        public void CanExecute_True_WhenNoCanExecuteProvided()
        {
            var cmd = new RelayCommand(() => { });

            cmd.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void CanExecute_False_WhenCanExecuteReturnsFalse()
        {
            var cmd = new RelayCommand(() => { }, () => false);

            cmd.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void RaiseCanExecuteChanged_FiresEvent()
        {
            var cmd = new RelayCommand(() => { });
            bool fired = false;
            cmd.CanExecuteChanged += (s, e) => fired = true;

            cmd.RaiseCanExecuteChanged();

            fired.Should().BeTrue();
        }

        [Fact]
        public void GenericRelayCommand_Execute_PassesParameter()
        {
            int received = 0;
            var cmd = new RelayCommand<int>(x => received = x);

            cmd.Execute(42);

            received.Should().Be(42);
        }
    }

    public class AsyncRelayCommandTests
    {
        [Fact]
        public async Task ExecuteAsync_CompletesTask()
        {
            bool completed = false;
            var cmd = new AsyncRelayCommand(async () =>
            {
                await Task.Delay(10);
                completed = true;
            });

            await cmd.ExecuteAsync(null);

            completed.Should().BeTrue();
        }

        [Fact]
        public async Task ExecuteAsync_SetsIsRunning()
        {
            var tcs = new TaskCompletionSource<bool>();
            bool wasRunningDuringExecution = false;
            var cmd = new AsyncRelayCommand(async () =>
            {
                await tcs.Task;
            });

            var execTask = cmd.ExecuteAsync(null);
            wasRunningDuringExecution = cmd.IsRunning;
            tcs.SetResult(true);
            await execTask;

            wasRunningDuringExecution.Should().BeTrue();
            cmd.IsRunning.Should().BeFalse();
        }
    }
}
