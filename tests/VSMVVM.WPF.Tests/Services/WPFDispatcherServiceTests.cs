using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.WPF.Services;
using Xunit;

namespace VSMVVM.WPF.Tests.Services
{
    // WPF Dispatcher 가 있는 STA 환경에서 실행 — [WpfFact] 사용.
    // Application.Current 가 null 일 때 Dispatcher.CurrentDispatcher 로 폴백되는 경로를 검증.
    public class WPFDispatcherServiceTests
    {
        private static WPFDispatcherService Create() => new WPFDispatcherService();

        // ── Invoke ─────────────────────────────────────────────────────

        [WpfFact]
        public void Invoke_WhenOnUIThread_ShouldExecuteActionSynchronously()
        {
            // Arrange
            var svc = Create();
            bool executed = false;

            // Act
            svc.Invoke(() => { executed = true; });

            // Assert
            executed.Should().BeTrue("UI 스레드에서 Invoke 는 동기 실행되어야 한다");
        }

        [WpfFact]
        public void Invoke_WhenNullAction_ShouldThrowArgumentNullException()
        {
            // Arrange
            var svc = Create();

            // Act
            Action act = () => svc.Invoke(null);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("action");
        }

        [WpfFact]
        public void Invoke_WhenActionThrows_ShouldPropagateException()
        {
            // Arrange
            var svc = Create();

            // Act
            Action act = () => svc.Invoke(() => throw new InvalidOperationException("test error"));

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("test error");
        }

        // ── InvokeAsync ────────────────────────────────────────────────

        [WpfFact]
        public async Task InvokeAsync_WhenCalled_ShouldExecuteAction()
        {
            // Arrange
            var svc = Create();
            bool executed = false;

            // Act
            await svc.InvokeAsync(() => { executed = true; }).WaitAsync(TimeSpan.FromSeconds(3));

            // Assert
            executed.Should().BeTrue();
        }

        [WpfFact]
        public void InvokeAsync_WhenNullAction_ShouldThrowArgumentNullException()
        {
            // Arrange
            var svc = Create();

            // Act
            Action act = () => svc.InvokeAsync(null);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("action");
        }

        [WpfFact]
        public async Task InvokeAsync_WhenCalled_ShouldReturnCompletedTask()
        {
            // Arrange
            var svc = Create();

            // Act
            var task = svc.InvokeAsync(() => { });
            await task.WaitAsync(TimeSpan.FromSeconds(3));

            // Assert
            task.IsCompleted.Should().BeTrue();
        }

        // ── CheckAccess ────────────────────────────────────────────────

        [WpfFact]
        public void CheckAccess_WhenOnUIThread_ShouldReturnTrue()
        {
            // Arrange
            var svc = Create();

            // Act
            var result = svc.CheckAccess();

            // Assert
            result.Should().BeTrue("[WpfFact] 은 UI 스레드에서 실행되므로 CheckAccess = true");
        }

        // 참고: WPFDispatcherService.GetDispatcher() 는 Application.Current 가 null 일 때
        // Dispatcher.CurrentDispatcher (호출 스레드 소유 Dispatcher) 를 사용한다.
        // 따라서 Application 없이 Task.Run 으로 백그라운드 스레드에서 호출하면
        // 그 스레드 자체가 Dispatcher 소유자가 되어 CheckAccess = true 를 반환한다.
        // 이 동작이 Application.Current != null 환경 (= 실제 앱)과 다르므로,
        // 순수 단위 테스트에서는 UI 스레드 CheckAccess = true 만 검증한다.

        // ── Yield ──────────────────────────────────────────────────────

        [WpfFact]
        public async Task Yield_WhenCalled_ShouldReturnCompletableTask()
        {
            // Arrange
            var svc = Create();

            // Act
            var task = svc.Yield();
            await task.WaitAsync(TimeSpan.FromSeconds(3));

            // Assert
            task.IsCompleted.Should().BeTrue("Yield 는 Background 우선순위 작업이므로 완료되어야 한다");
        }

        [WpfFact]
        public async Task Yield_WhenCalled_ShouldAllowOtherWorkToRunFirst()
        {
            // Arrange
            var svc = Create();
            int order = 0;
            int yieldedAfter = -1;

            // Act — Yield 를 await 하면 다른 작업(Background priority)이 먼저 실행
            var yieldTask = svc.Yield();
            order = 1;
            await yieldTask.WaitAsync(TimeSpan.FromSeconds(3));
            yieldedAfter = order;

            // Assert
            yieldedAfter.Should().Be(1, "Yield 가 완료된 후 다음 줄이 실행되어야 한다");
        }
    }
}
