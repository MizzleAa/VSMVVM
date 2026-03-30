using System;
using System.Collections.Generic;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.Logging
{
    #region Mock Logger

    /// <summary>
    /// ILoggerService Mock 구현. 호출 기록을 캡처합니다.
    /// </summary>
    public class MockLoggerService : ILoggerService
    {
        public record LogEntry(string Level, string Message, Exception Exception);

        public List<LogEntry> Logs { get; } = new();
        public string ConfigPath { get; private set; }

        public void Configure(string configFilePath)
        {
            ConfigPath = configFilePath;
        }

        public void Trace(string message) => Logs.Add(new LogEntry("Trace", message, null));
        public void Debug(string message) => Logs.Add(new LogEntry("Debug", message, null));
        public void Info(string message) => Logs.Add(new LogEntry("Info", message, null));
        public void Warn(string message) => Logs.Add(new LogEntry("Warn", message, null));
        public void Error(string message) => Logs.Add(new LogEntry("Error", message, null));
        public void Error(string message, Exception exception) => Logs.Add(new LogEntry("Error", message, exception));
        public void Fatal(string message) => Logs.Add(new LogEntry("Fatal", message, null));
        public void Fatal(string message, Exception exception) => Logs.Add(new LogEntry("Fatal", message, exception));
    }

    #endregion

    public class LoggerServiceTests
    {
        private readonly MockLoggerService _logger = new();

        #region Configure

        [Fact]
        public void Configure_StoresConfigPath()
        {
            _logger.Configure("nlog.config");

            _logger.ConfigPath.Should().Be("nlog.config");
        }

        [Fact]
        public void Configure_Override_TakesLatestPath()
        {
            _logger.Configure("first.config");
            _logger.Configure("second.config");

            _logger.ConfigPath.Should().Be("second.config");
        }

        #endregion

        #region Log Level Methods

        [Fact]
        public void Trace_RecordsCorrectLevel()
        {
            _logger.Trace("trace msg");

            _logger.Logs.Should().ContainSingle()
                .Which.Level.Should().Be("Trace");
        }

        [Fact]
        public void Debug_RecordsCorrectLevel()
        {
            _logger.Debug("debug msg");

            _logger.Logs.Should().ContainSingle()
                .Which.Level.Should().Be("Debug");
        }

        [Fact]
        public void Info_RecordsCorrectLevel()
        {
            _logger.Info("info msg");

            _logger.Logs.Should().ContainSingle()
                .Which.Level.Should().Be("Info");
        }

        [Fact]
        public void Warn_RecordsCorrectLevel()
        {
            _logger.Warn("warn msg");

            _logger.Logs.Should().ContainSingle()
                .Which.Level.Should().Be("Warn");
        }

        [Fact]
        public void Error_RecordsCorrectLevel()
        {
            _logger.Error("error msg");

            _logger.Logs.Should().ContainSingle()
                .Which.Level.Should().Be("Error");
        }

        [Fact]
        public void Fatal_RecordsCorrectLevel()
        {
            _logger.Fatal("fatal msg");

            _logger.Logs.Should().ContainSingle()
                .Which.Level.Should().Be("Fatal");
        }

        #endregion

        #region Message Content

        [Fact]
        public void LogMethods_PreserveMessage()
        {
            _logger.Info("Hello World");

            _logger.Logs.Should().ContainSingle()
                .Which.Message.Should().Be("Hello World");
        }

        [Fact]
        public void LogMethods_MultipleMessages_PreserveOrder()
        {
            _logger.Info("first");
            _logger.Warn("second");
            _logger.Error("third");

            _logger.Logs.Should().HaveCount(3);
            _logger.Logs[0].Message.Should().Be("first");
            _logger.Logs[1].Message.Should().Be("second");
            _logger.Logs[2].Message.Should().Be("third");
        }

        #endregion

        #region Exception Logging

        [Fact]
        public void Error_WithException_RecordsException()
        {
            var ex = new InvalidOperationException("test error");

            _logger.Error("Something failed", ex);

            _logger.Logs.Should().ContainSingle();
            _logger.Logs[0].Exception.Should().BeSameAs(ex);
            _logger.Logs[0].Message.Should().Be("Something failed");
        }

        [Fact]
        public void Fatal_WithException_RecordsException()
        {
            var ex = new OutOfMemoryException("no memory");

            _logger.Fatal("Critical failure", ex);

            _logger.Logs.Should().ContainSingle();
            _logger.Logs[0].Exception.Should().BeSameAs(ex);
            _logger.Logs[0].Level.Should().Be("Fatal");
        }

        [Fact]
        public void Error_WithoutException_HasNullException()
        {
            _logger.Error("simple error");

            _logger.Logs.Should().ContainSingle()
                .Which.Exception.Should().BeNull();
        }

        #endregion

        #region DI Integration

        [Fact]
        public void LoggerService_RegisterAndResolve_AsSingleton()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerService, MockLoggerService>();
            var container = sc.CreateContainer();

            var logger = container.GetService<ILoggerService>();

            logger.Should().NotBeNull();
            logger.Should().BeOfType<MockLoggerService>();
        }

        [Fact]
        public void LoggerService_Singleton_ReturnsSameInstance()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerService, MockLoggerService>();
            var container = sc.CreateContainer();

            var a = container.GetService<ILoggerService>();
            var b = container.GetService<ILoggerService>();

            a.Should().BeSameAs(b);
        }

        [Fact]
        public void LoggerService_Instance_ResolvesSameObject()
        {
            var sc = new ServiceCollection();
            var instance = new MockLoggerService();
            sc.AddSingleton<ILoggerService, MockLoggerService>(instance);
            var container = sc.CreateContainer();

            var resolved = container.GetService<ILoggerService>();

            resolved.Should().BeSameAs(instance);
        }

        [Fact]
        public void LoggerService_ResolvedFromDI_FunctionsCorrectly()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerService, MockLoggerService>();
            var container = sc.CreateContainer();

            var logger = container.GetService<ILoggerService>();
            logger.Info("DI resolved logger works");

            var mock = (MockLoggerService)logger;
            mock.Logs.Should().ContainSingle()
                .Which.Message.Should().Be("DI resolved logger works");
        }

        #endregion

        #region Constructor Injection

        public class ServiceWithLogger
        {
            public ILoggerService Logger { get; }
            public ServiceWithLogger(ILoggerService logger) => Logger = logger;
        }

        [Fact]
        public void LoggerService_ConstructorInjection_Resolves()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerService, MockLoggerService>();
            sc.AddTransient<ServiceWithLogger>();
            var container = sc.CreateContainer();

            var service = container.GetService<ServiceWithLogger>();

            service.Logger.Should().NotBeNull();
            service.Logger.Should().BeOfType<MockLoggerService>();
        }

        [Fact]
        public void LoggerService_ConstructorInjection_IsSameInstance()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ILoggerService, MockLoggerService>();
            sc.AddTransient<ServiceWithLogger>();
            var container = sc.CreateContainer();

            var s1 = container.GetService<ServiceWithLogger>();
            var s2 = container.GetService<ServiceWithLogger>();

            // ServiceWithLogger는 Transient이지만 Logger는 Singleton
            s1.Logger.Should().BeSameAs(s2.Logger);
        }

        #endregion
    }
}
