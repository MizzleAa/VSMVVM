using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using VSMVVM.Core.Attributes;
using Xunit;

namespace VSMVVM.Core.Tests.Logging
{
    #region Test Helpers

    /// <summary>
    /// [Log] 테스트용 타겟 클래스.
    /// </summary>
    public class LogTargetSample
    {
        [Log]
        public void DefaultLevel() { }

        [Log(Level = LogLevel.Debug)]
        public void DebugLevel() { }

        [Log(Level = LogLevel.Trace)]
        public void TraceLevel() { }

        [Log(Level = LogLevel.Warn)]
        public void WarnLevel() { }

        [Log(Level = LogLevel.Error)]
        public void ErrorLevel() { }
    }

    #endregion

    public class LogAttributeTests
    {
        #region LogLevel Enum

        [Fact]
        public void LogLevel_HasExpectedValues()
        {
            var values = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().ToList();

            values.Should().HaveCount(5);
            values.Should().Contain(LogLevel.Trace);
            values.Should().Contain(LogLevel.Debug);
            values.Should().Contain(LogLevel.Info);
            values.Should().Contain(LogLevel.Warn);
            values.Should().Contain(LogLevel.Error);
        }

        [Fact]
        public void LogLevel_HasCorrectOrdinalOrder()
        {
            // Trace(0) < Debug(1) < Info(2) < Warn(3) < Error(4)
            ((int)LogLevel.Trace).Should().Be(0);
            ((int)LogLevel.Debug).Should().Be(1);
            ((int)LogLevel.Info).Should().Be(2);
            ((int)LogLevel.Warn).Should().Be(3);
            ((int)LogLevel.Error).Should().Be(4);
        }

        #endregion

        #region Attribute Defaults

        [Fact]
        public void LogAttribute_DefaultLevel_IsInfo()
        {
            var attr = new LogAttribute();

            attr.Level.Should().Be(LogLevel.Info);
        }

        [Fact]
        public void LogAttribute_Level_CanBeSet()
        {
            var attr = new LogAttribute { Level = LogLevel.Error };

            attr.Level.Should().Be(LogLevel.Error);
        }

        #endregion

        #region Attribute Usage

        [Fact]
        public void LogAttribute_TargetsMethodOnly()
        {
            var usage = typeof(LogAttribute)
                .GetCustomAttribute<AttributeUsageAttribute>();

            usage.Should().NotBeNull();
            usage.ValidOn.Should().Be(AttributeTargets.Method);
        }

        [Fact]
        public void LogAttribute_DoesNotAllowMultiple()
        {
            var usage = typeof(LogAttribute)
                .GetCustomAttribute<AttributeUsageAttribute>();

            usage.Should().NotBeNull();
            usage.AllowMultiple.Should().BeFalse();
        }

        [Fact]
        public void LogAttribute_IsNotInherited()
        {
            var usage = typeof(LogAttribute)
                .GetCustomAttribute<AttributeUsageAttribute>();

            usage.Should().NotBeNull();
            usage.Inherited.Should().BeFalse();
        }

        #endregion

        #region Reflection Discovery

        [Fact]
        public void Method_WithDefaultLog_ReturnsInfoLevel()
        {
            var method = typeof(LogTargetSample).GetMethod(nameof(LogTargetSample.DefaultLevel));
            var attr = method.GetCustomAttribute<LogAttribute>();

            attr.Should().NotBeNull();
            attr.Level.Should().Be(LogLevel.Info);
        }

        [Fact]
        public void Method_WithDebugLog_ReturnsDebugLevel()
        {
            var method = typeof(LogTargetSample).GetMethod(nameof(LogTargetSample.DebugLevel));
            var attr = method.GetCustomAttribute<LogAttribute>();

            attr.Should().NotBeNull();
            attr.Level.Should().Be(LogLevel.Debug);
        }

        [Theory]
        [InlineData(nameof(LogTargetSample.TraceLevel), LogLevel.Trace)]
        [InlineData(nameof(LogTargetSample.DebugLevel), LogLevel.Debug)]
        [InlineData(nameof(LogTargetSample.DefaultLevel), LogLevel.Info)]
        [InlineData(nameof(LogTargetSample.WarnLevel), LogLevel.Warn)]
        [InlineData(nameof(LogTargetSample.ErrorLevel), LogLevel.Error)]
        public void Method_WithLogAttribute_ReturnsCorrectLevel(string methodName, LogLevel expectedLevel)
        {
            var method = typeof(LogTargetSample).GetMethod(methodName);
            var attr = method.GetCustomAttribute<LogAttribute>();

            attr.Should().NotBeNull();
            attr.Level.Should().Be(expectedLevel);
        }

        [Fact]
        public void DiscoverAllLogMethods_ReturnsExpectedCount()
        {
            var logMethods = typeof(LogTargetSample)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<LogAttribute>() != null)
                .ToList();

            logMethods.Should().HaveCount(5);
        }

        #endregion
    }
}
