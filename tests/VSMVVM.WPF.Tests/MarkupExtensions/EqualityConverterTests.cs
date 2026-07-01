using System;
using System.Globalization;
using FluentAssertions;
using VSMVVM.WPF.MarkupExtensions;
using Xunit;

namespace VSMVVM.WPF.Tests.MarkupExtensions
{
    public class EqualityConverterTests
    {
        private readonly EqualityConverter _sut = EqualityConverter.Instance;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        // ── Convert ──────────────────────────────────────────────────

        [Fact]
        public void Convert_WhenBothValuesEqual_ShouldReturnTrue()
        {
            // Arrange
            object[] values = { "hello", "hello" };

            // Act
            var result = _sut.Convert(values, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void Convert_WhenValuesNotEqual_ShouldReturnFalse()
        {
            // Arrange
            object[] values = { "hello", "world" };

            // Act
            var result = _sut.Convert(values, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void Convert_WhenBothNull_ShouldReturnTrue()
        {
            // Arrange
            object[] values = { null, null };

            // Act
            var result = _sut.Convert(values, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void Convert_WhenOneNullAndOneNot_ShouldReturnFalse()
        {
            // Arrange
            object[] values = { null, "value" };

            // Act
            var result = _sut.Convert(values, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void Convert_WhenIntegersEqual_ShouldReturnTrue()
        {
            // Arrange
            object[] values = { 42, 42 };

            // Act
            var result = _sut.Convert(values, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void Convert_WhenIntegersNotEqual_ShouldReturnFalse()
        {
            // Arrange
            object[] values = { 42, 43 };

            // Act
            var result = _sut.Convert(values, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void Convert_WhenNullArray_ShouldReturnFalse()
        {
            // Arrange / Act
            var result = _sut.Convert(null, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void Convert_WhenOnlyOneValue_ShouldReturnFalse()
        {
            // Arrange
            object[] values = { "single" };

            // Act
            var result = _sut.Convert(values, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(false, "values.Length < 2 이면 false 반환");
        }

        [Fact]
        public void Convert_WhenEmptyArray_ShouldReturnFalse()
        {
            // Arrange
            object[] values = { };

            // Act
            var result = _sut.Convert(values, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void Convert_WhenEnumValuesEqual_ShouldReturnTrue()
        {
            // Arrange
            object[] values = { DayOfWeek.Monday, DayOfWeek.Monday };

            // Act
            var result = _sut.Convert(values, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(true);
        }

        // ── ConvertBack ───────────────────────────────────────────────

        [Fact]
        public void ConvertBack_ShouldThrowNotSupportedException()
        {
            // Arrange / Act
            Action act = () => _sut.ConvertBack(true, new[] { typeof(object), typeof(object) }, null, _culture);

            // Assert
            act.Should().Throw<NotSupportedException>();
        }

        // ── 싱글톤 인스턴스 ──────────────────────────────────────────

        [Fact]
        public void Instance_ShouldBeSingleton()
        {
            // Assert
            EqualityConverter.Instance.Should().BeSameAs(EqualityConverter.Instance);
        }
    }
}
