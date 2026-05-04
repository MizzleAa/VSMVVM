using System;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.MVVM
{
    /// <summary>
    /// 회귀 방지: ICommand.Execute는 async void 시그니처를 피할 수 없으므로
    /// 사용자 람다에서 던진 예외가 SynchronizationContext로 전파되어
    /// 호출자/디스패처를 죽이는 일이 없어야 한다.
    /// </summary>
    public class AsyncRelayCommandRegressionTests
    {
        [Fact]
        public async Task Execute_Swallows_Exception_From_User_Lambda()
        {
            var command = new AsyncRelayCommand(() => throw new InvalidOperationException("boom"));

            // ICommand.Execute는 void 반환이라 호출 직후 await할 수 없으므로
            // IsRunning이 finally로 false 복원될 때까지 잠시 yield한다.
            command.Execute(null);
            await Task.Yield();
            await Task.Delay(20);

            command.IsRunning.Should().BeFalse("finally 블록은 예외와 무관하게 실행되어야 함");
        }

        [Fact]
        public async Task Execute_Generic_Swallows_Exception_From_User_Lambda()
        {
            var command = new AsyncRelayCommand<string>(arg => throw new InvalidOperationException("boom"));

            command.Execute("anything");
            await Task.Yield();
            await Task.Delay(20);

            command.IsRunning.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteAsync_Still_Propagates_Exception_To_Awaiter()
        {
            // ICommand.Execute는 swallow하지만, 사용자가 직접 await 하는 ExecuteAsync는
            // 정상적으로 예외가 전파되어야 한다 (그래야 try/catch가 가능).
            var command = new AsyncRelayCommand(() => throw new InvalidOperationException("boom"));

            Func<Task> act = () => command.ExecuteAsync(null);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
            command.IsRunning.Should().BeFalse();
        }

        [Fact]
        public async Task IsRunning_Toggles_Around_Successful_Execution()
        {
            var tcs = new TaskCompletionSource<bool>();
            var command = new AsyncRelayCommand(() => tcs.Task);

            command.IsRunning.Should().BeFalse();

            var task = command.ExecuteAsync(null);
            command.IsRunning.Should().BeTrue();

            tcs.SetResult(true);
            await task;

            command.IsRunning.Should().BeFalse();
        }
    }
}
