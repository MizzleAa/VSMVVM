using System;
using FluentAssertions;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.Logging
{
    #region Test ViewModels

    /// <summary>
    /// [Property] + [Log] / ObservableValidator + [Log] 검증용 ViewModel.
    /// </summary>
    public partial class LogPropertyTargetViewModel : ViewModelBase
    {
        [Property]
        [Log]
        private string _name;

        [Property]
        [Log(Level = LogLevel.Debug)]
        private int _count;

        [Property]
        private string _untracked;

        // PropertyChangedFor 와 함께 — setter 안 조합 동작 회귀 확인.
        [Property]
        [Log]
        [PropertyChangedFor(nameof(FullName))]
        private string _firstName;

        public string FullName => $"{FirstName ?? string.Empty}";
    }

    public partial class LogPropertyValidatorViewModel : ObservableValidator
    {
        [Property]
        [Log]
        private string _validatedName;

        // ObservableValidator + dependents 없음 (SetProperty 단축 분기).
        [Property]
        [Log(Level = LogLevel.Warn)]
        private int _validatedCount;

        [Property]
        private string _untrackedValidator;
    }

    #endregion

    /// <summary>
    /// [Property] + [Log] Source Generator 통합 검증.
    /// ServiceLocator 정적 상태를 사용 — RegionManagerTests / LogAttributeRelayCommandTests 와 동일 collection.
    /// </summary>
    [Collection("ServiceLocator")]
    public class LogAttributePropertyTests : IDisposable
    {
        private readonly MockLoggerService _logger = new();

        public LogAttributePropertyTests()
        {
            ServiceLocator.SetServiceProvider(new TestContainer(_logger));
        }

        public void Dispose()
        {
            ServiceLocator.SetServiceProvider(new TestContainer(new MockLoggerService()));
        }

        private sealed class TestContainer : IServiceContainer
        {
            private readonly ILoggerService _logger;
            public TestContainer(ILoggerService logger) { _logger = logger; }
            public TInterface GetService<TInterface>() where TInterface : class => (TInterface)(object)_logger;
            public object GetService(Type serviceType) => serviceType == typeof(ILoggerService) ? _logger : null;
            public object GetService(string typeName) => null;
            public Type KeyType(string key) => null;
        }

        #region ViewModelBase + [Property] + [Log]

        [Fact]
        public void Property_WithLog_LogsOnChange()
        {
            var vm = new LogPropertyTargetViewModel();

            vm.Name = "Alice";

            _logger.Logs.Should().HaveCount(1);
            _logger.Logs[0].Level.Should().Be("Info");
            _logger.Logs[0].Message.Should().Contain("LogPropertyTargetViewModel.Name");
            _logger.Logs[0].Message.Should().Contain("Alice");
        }

        [Fact]
        public void Property_WithLogDebugLevel_LogsAtDebug()
        {
            var vm = new LogPropertyTargetViewModel();

            vm.Count = 42;

            _logger.Logs.Should().HaveCount(1);
            _logger.Logs[0].Level.Should().Be("Debug");
            _logger.Logs[0].Message.Should().Contain("Count");
            _logger.Logs[0].Message.Should().Contain("42");
        }

        [Fact]
        public void Property_WithLog_DoesNotLogOnSameValue()
        {
            var vm = new LogPropertyTargetViewModel { Name = "Alice" };
            _logger.Logs.Clear();

            vm.Name = "Alice"; // EqualityComparer 가드로 setter 본문 진입 안 함.

            _logger.Logs.Should().BeEmpty();
        }

        [Fact]
        public void Property_WithoutLog_DoesNotLog()
        {
            var vm = new LogPropertyTargetViewModel();

            vm.Untracked = "ignored";

            _logger.Logs.Should().BeEmpty();
        }

        [Fact]
        public void Property_WithLogAndPropertyChangedFor_StillLogsAndFiresDependent()
        {
            var vm = new LogPropertyTargetViewModel();
            string fullNameNotified = null;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LogPropertyTargetViewModel.FullName))
                    fullNameNotified = vm.FullName;
            };

            vm.FirstName = "Bob";

            _logger.Logs.Should().Contain(l => l.Message.Contains("FirstName") && l.Message.Contains("Bob"));
            fullNameNotified.Should().Be("Bob");
        }

        #endregion

        #region ObservableValidator + [Property] + [Log]

        [Fact]
        public void ObservableValidator_PropertyWithLog_LogsOnChange()
        {
            var vm = new LogPropertyValidatorViewModel();

            vm.ValidatedName = "Charlie";

            _logger.Logs.Should().HaveCount(1);
            _logger.Logs[0].Level.Should().Be("Info");
            _logger.Logs[0].Message.Should().Contain("ValidatedName");
            _logger.Logs[0].Message.Should().Contain("Charlie");
        }

        [Fact]
        public void ObservableValidator_PropertyWithLogWarn_LogsAtWarn()
        {
            var vm = new LogPropertyValidatorViewModel();

            vm.ValidatedCount = 7;

            _logger.Logs.Should().HaveCount(1);
            _logger.Logs[0].Level.Should().Be("Warn");
            _logger.Logs[0].Message.Should().Contain("ValidatedCount");
            _logger.Logs[0].Message.Should().Contain("7");
        }

        [Fact]
        public void ObservableValidator_PropertyWithLog_DoesNotLogOnSameValue()
        {
            var vm = new LogPropertyValidatorViewModel { ValidatedName = "Charlie" };
            _logger.Logs.Clear();

            vm.ValidatedName = "Charlie";

            _logger.Logs.Should().BeEmpty();
        }

        [Fact]
        public void ObservableValidator_PropertyWithoutLog_DoesNotLog()
        {
            var vm = new LogPropertyValidatorViewModel();

            vm.UntrackedValidator = "ignored";

            _logger.Logs.Should().BeEmpty();
        }

        #endregion
    }
}
