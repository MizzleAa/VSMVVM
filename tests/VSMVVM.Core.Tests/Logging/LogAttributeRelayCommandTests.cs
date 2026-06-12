using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.Logging
{
    #region Test Helpers

    /// <summary>
    /// 테스트용 ServiceContainer — ILoggerService 만 해석.
    /// ServiceLocator 에 주입해 Source Generator 가 생성한 람다가 이 컨테이너에서 로거를 가져오게 한다.
    /// </summary>
    internal sealed class StubServiceContainer : IServiceContainer
    {
        private readonly ILoggerService _logger;

        public StubServiceContainer(ILoggerService logger) { _logger = logger; }

        public TInterface GetService<TInterface>() where TInterface : class
            => (TInterface)(object)_logger;

        public object GetService(Type serviceType)
            => serviceType == typeof(ILoggerService) ? _logger : null;

        public object GetService(string typeName) => null;

        public Type KeyType(string key) => null;
    }

    /// <summary>
    /// [RelayCommand] + [Log] / [AsyncRelayCommand] + [Log] 조합 검증용 ViewModel.
    /// Source Generator 가 각 메서드에 대해 *Command 프로퍼티를 생성한다.
    /// </summary>
    public partial class LogTargetViewModel : ViewModelBase
    {
        public int SyncRelayExecutions { get; private set; }
        public int? SyncRelayParam { get; private set; }
        public int SyncRelayWithoutLogExecutions { get; private set; }
        public int AsyncRelayExecutions { get; private set; }
        public int? AsyncRelayParam { get; private set; }
        public bool ThrowOnExecute { get; set; }

        [RelayCommand]
        [Log]
        private void DoSyncWithLog() { SyncRelayExecutions++; }

        [RelayCommand]
        [Log(Level = LogLevel.Debug)]
        private void DoSyncWithDebug() { SyncRelayExecutions++; }

        [RelayCommand]
        private void DoSyncWithoutLog() { SyncRelayWithoutLogExecutions++; }

        [RelayCommand]
        [Log]
        private void DoSyncWithLogParam(int value)
        {
            SyncRelayExecutions++;
            SyncRelayParam = value;
        }

        [RelayCommand]
        [Log]
        private void DoSyncWithLogParamNullable(string value)
        {
            SyncRelayExecutions++;
        }

        [RelayCommand]
        [Log]
        private void DoSyncThatThrows()
        {
            if (ThrowOnExecute) throw new InvalidOperationException("boom");
            SyncRelayExecutions++;
        }

        [AsyncRelayCommand]
        [Log(Level = LogLevel.Warn)]
        private async Task DoAsyncWithLog()
        {
            await Task.Yield();
            AsyncRelayExecutions++;
        }

        [AsyncRelayCommand]
        [Log]
        private async Task DoAsyncWithLogParam(int value)
        {
            await Task.Yield();
            AsyncRelayExecutions++;
            AsyncRelayParam = value;
        }
    }

    #endregion

    /// <summary>
    /// [Log] Source Generator 통합 검증. Mock 로거를 ServiceLocator 에 주입해서
    /// 생성된 Command 람다가 의도대로 ILoggerService 를 호출하는지 확인.
    /// </summary>
    [Collection("ServiceLocator")] // ServiceLocator 가 정적 상태라 병렬 실행 시 충돌 — 직렬화.
    public class LogAttributeRelayCommandTests : IDisposable
    {
        private readonly MockLoggerService _logger = new();
        private readonly LogTargetViewModel _vm = new();

        public LogAttributeRelayCommandTests()
        {
            ServiceLocator.SetServiceProvider(new StubServiceContainer(_logger));
        }

        public void Dispose()
        {
            // 다른 테스트로 누수되지 않게 정리 — 현재 ServiceLocator API 가 reset 을 직접 노출하지 않아
            // 더미 컨테이너로 덮어쓴다 (테스트 격리용).
            ServiceLocator.SetServiceProvider(new StubServiceContainer(new MockLoggerService()));
        }

        #region [RelayCommand] + [Log]

        [Fact]
        public void SyncRelay_WithLog_InvokesLoggerOnceAtInfoLevel()
        {
            _vm.DoSyncWithLogCommand.Execute(null);

            _vm.SyncRelayExecutions.Should().Be(1);
            _logger.Logs.Should().HaveCount(1);
            _logger.Logs[0].Level.Should().Be("Info");
            _logger.Logs[0].Message.Should().Contain("DoSyncWithLog");
            _logger.Logs[0].Message.Should().Contain("LogTargetViewModel");
        }

        [Fact]
        public void SyncRelay_WithLogDebugLevel_LogsAtDebug()
        {
            _vm.DoSyncWithDebugCommand.Execute(null);

            _logger.Logs.Should().HaveCount(1);
            _logger.Logs[0].Level.Should().Be("Debug");
        }

        [Fact]
        public void SyncRelay_WithoutLog_DoesNotInvokeLogger()
        {
            _vm.DoSyncWithoutLogCommand.Execute(null);

            _vm.SyncRelayWithoutLogExecutions.Should().Be(1);
            _logger.Logs.Should().BeEmpty();
        }

        [Fact]
        public void SyncRelay_WithLogParam_PassesParameterAndLogs()
        {
            _vm.DoSyncWithLogParamCommand.Execute(7);

            _vm.SyncRelayParam.Should().Be(7);
            _logger.Logs.Should().HaveCount(1);
            _logger.Logs[0].Message.Should().Contain("DoSyncWithLogParam");
            // 1.2.3: 파라미터 값이 메시지에 보간되어 어느 항목을 클릭했는지 식별 가능.
            _logger.Logs[0].Message.Should().Contain("(7)");
        }

        [Fact]
        public void SyncRelay_WithLogParam_NullParameter_LogsAsEmpty()
        {
            // null 파라미터도 보간식이 throw 하지 않고 빈 문자열로 처리되어야 한다.
            _vm.DoSyncWithLogParamNullableCommand.Execute(null);

            _logger.Logs.Should().HaveCount(1);
            _logger.Logs[0].Message.Should().Contain("DoSyncWithLogParamNullable");
            _logger.Logs[0].Message.Should().Contain("()");
        }

        [Fact]
        public void SyncRelay_MethodThrows_LogStillRecorded()
        {
            _vm.ThrowOnExecute = true;

            Action act = () => _vm.DoSyncThatThrowsCommand.Execute(null);

            // 메서드 자체의 예외는 그대로 전파 — 람다 안 try/catch 는 로거 호출 실패만 잡는다.
            act.Should().Throw<InvalidOperationException>();
            _logger.Logs.Should().HaveCount(1);
        }

        #endregion

        #region [AsyncRelayCommand] + [Log]

        [Fact]
        public async Task AsyncRelay_WithLog_InvokesLoggerAtWarnLevel()
        {
            await _vm.DoAsyncWithLogCommand.ExecuteAsync(null);

            _vm.AsyncRelayExecutions.Should().Be(1);
            _logger.Logs.Should().HaveCount(1);
            _logger.Logs[0].Level.Should().Be("Warn");
            _logger.Logs[0].Message.Should().Contain("DoAsyncWithLog");
        }

        [Fact]
        public async Task AsyncRelay_WithLogParam_PassesParameterAndLogs()
        {
            await _vm.DoAsyncWithLogParamCommand.ExecuteAsync(42);

            _vm.AsyncRelayParam.Should().Be(42);
            _logger.Logs.Should().HaveCount(1);
            _logger.Logs[0].Level.Should().Be("Info");
            // 1.2.3: 파라미터 값이 메시지에 보간.
            _logger.Logs[0].Message.Should().Contain("(42)");
        }

        #endregion

        #region ServiceLocator 미초기화 안전성

        [Fact]
        public void SyncRelay_WithLog_DoesNotThrow_WhenLoggerNotResolvable()
        {
            // ServiceLocator 가 ILoggerService 를 못 찾는 컨테이너로 교체
            ServiceLocator.SetServiceProvider(new EmptyContainer());

            Action act = () => _vm.DoSyncWithLogCommand.Execute(null);

            act.Should().NotThrow();
            _vm.SyncRelayExecutions.Should().Be(1);
        }

        private sealed class EmptyContainer : IServiceContainer
        {
            public TInterface GetService<TInterface>() where TInterface : class => null;
            public object GetService(Type serviceType) => null;
            public object GetService(string typeName) => null;
            public Type KeyType(string key) => null;
        }

        #endregion
    }

    [CollectionDefinition("ServiceLocator")]
    public class ServiceLocatorCollection { }
}
