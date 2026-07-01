using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using VSMVVM.WPF.Controls;
using Xunit;
using Xunit.Abstractions;

namespace VSMVVM.WPF.Tests.Controls
{
    // ══════════════════════════════════════════════════════════════
    //  BoolToVisibilityConverter 테스트 — 순수 단위 (STA 불필요)
    // ══════════════════════════════════════════════════════════════
    public class BoolToVisibilityConverterTests
    {
        private readonly BoolToVisibilityConverter _sut = BoolToVisibilityConverter.Instance;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        [Fact]
        public void Convert_WhenTrue_ShouldReturnVisible()
        {
            // Arrange / Act
            var result = _sut.Convert(true, typeof(Visibility), null, _culture);

            // Assert
            result.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void Convert_WhenFalse_ShouldReturnCollapsed()
        {
            // Arrange / Act
            var result = _sut.Convert(false, typeof(Visibility), null, _culture);

            // Assert
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_WhenNullValue_ShouldReturnCollapsed()
        {
            // Arrange / Act — null 은 false 로 처리
            var result = _sut.Convert(null, typeof(Visibility), null, _culture);

            // Assert
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_WhenNonBoolValue_ShouldReturnCollapsed()
        {
            // Arrange / Act — 문자열 "true" 는 bool 이 아니므로 false 취급
            var result = _sut.Convert("true", typeof(Visibility), null, _culture);

            // Assert
            result.Should().Be(Visibility.Collapsed);
        }

        [Theory]
        [InlineData("Inverse")]
        [InlineData("inverse")]
        [InlineData("Invert")]
        [InlineData("!")]
        public void Convert_WhenInverseParameter_WhenTrue_ShouldReturnCollapsed(string parameter)
        {
            // Arrange / Act
            var result = _sut.Convert(true, typeof(Visibility), parameter, _culture);

            // Assert
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_WhenInverseParameter_WhenFalse_ShouldReturnVisible()
        {
            // Arrange / Act
            var result = _sut.Convert(false, typeof(Visibility), "Inverse", _culture);

            // Assert
            result.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void ConvertBack_WhenVisible_ShouldReturnTrue()
        {
            // Arrange / Act
            var result = _sut.ConvertBack(Visibility.Visible, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void ConvertBack_WhenCollapsed_ShouldReturnFalse()
        {
            // Arrange / Act
            var result = _sut.ConvertBack(Visibility.Collapsed, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void ConvertBack_WhenHidden_ShouldReturnFalse()
        {
            // Arrange / Act
            var result = _sut.ConvertBack(Visibility.Hidden, typeof(bool), null, _culture);

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void ConvertBack_WhenInverseParameter_WhenVisible_ShouldReturnFalse()
        {
            // Arrange / Act
            var result = _sut.ConvertBack(Visibility.Visible, typeof(bool), "Inverse", _culture);

            // Assert
            result.Should().Be(false);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  BoolToVisibilityInverseConverter 테스트
    // ══════════════════════════════════════════════════════════════
    public class BoolToVisibilityInverseConverterTests
    {
        private readonly BoolToVisibilityInverseConverter _sut = BoolToVisibilityInverseConverter.Instance;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        [Fact]
        public void Convert_WhenTrue_ShouldReturnCollapsed()
        {
            var result = _sut.Convert(true, typeof(Visibility), null, _culture);
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_WhenFalse_ShouldReturnVisible()
        {
            var result = _sut.Convert(false, typeof(Visibility), null, _culture);
            result.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void ConvertBack_WhenVisible_ShouldReturnFalse()
        {
            var result = _sut.ConvertBack(Visibility.Visible, typeof(bool), null, _culture);
            result.Should().Be(false);
        }

        [Fact]
        public void ConvertBack_WhenCollapsed_ShouldReturnTrue()
        {
            var result = _sut.ConvertBack(Visibility.Collapsed, typeof(bool), null, _culture);
            result.Should().Be(true);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  BoolToOpacityConverter 테스트
    // ══════════════════════════════════════════════════════════════
    public class BoolToOpacityConverterTests
    {
        private readonly BoolToOpacityConverter _sut = BoolToOpacityConverter.Instance;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        [Fact]
        public void Convert_WhenTrue_ShouldReturnFullOpacity()
        {
            // Arrange / Act
            var result = _sut.Convert(true, typeof(double), null, _culture);

            // Assert
            result.Should().Be(1.0);
        }

        [Fact]
        public void Convert_WhenFalse_ShouldReturnDimOpacity()
        {
            // Arrange / Act
            var result = _sut.Convert(false, typeof(double), null, _culture);

            // Assert
            result.Should().Be(0.3);
        }

        [Fact]
        public void Convert_WhenNull_ShouldReturnDimOpacity()
        {
            // Arrange / Act
            var result = _sut.Convert(null, typeof(double), null, _culture);

            // Assert
            result.Should().Be(0.3);
        }

        [Fact]
        public void ConvertBack_ShouldThrowNotSupportedException()
        {
            // Arrange / Act
            Action act = () => _sut.ConvertBack(1.0, typeof(bool), null, _culture);

            // Assert
            act.Should().Throw<NotSupportedException>();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  GreaterThanOneConverter 테스트
    // ══════════════════════════════════════════════════════════════
    public class GreaterThanOneConverterTests
    {
        private readonly GreaterThanOneConverter _sut = GreaterThanOneConverter.Instance;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        [Fact]
        public void Convert_WhenIntGreaterThanOne_ShouldReturnTrue()
        {
            var result = _sut.Convert(2, typeof(bool), null, _culture);
            result.Should().Be(true);
        }

        [Fact]
        public void Convert_WhenIntIsOne_ShouldReturnFalse()
        {
            var result = _sut.Convert(1, typeof(bool), null, _culture);
            result.Should().Be(false);
        }

        [Fact]
        public void Convert_WhenIntIsZero_ShouldReturnFalse()
        {
            var result = _sut.Convert(0, typeof(bool), null, _culture);
            result.Should().Be(false);
        }

        [Fact]
        public void Convert_WhenLong_ShouldHandleLongType()
        {
            var result = _sut.Convert(3L, typeof(bool), null, _culture);
            result.Should().Be(true);
        }

        [Fact]
        public void Convert_WhenNullOrUnknownType_ShouldReturnFalse()
        {
            // Arrange / Act
            var r1 = _sut.Convert(null, typeof(bool), null, _culture);
            var r2 = _sut.Convert("notAnInt", typeof(bool), null, _culture);

            // Assert
            r1.Should().Be(false);
            r2.Should().Be(false);
        }

        [Fact]
        public void ConvertBack_ShouldThrowNotSupportedException()
        {
            Action act = () => _sut.ConvertBack(true, typeof(int), null, _culture);
            act.Should().Throw<NotSupportedException>();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  ZeroIndexToCollapsedConverter 테스트
    // ══════════════════════════════════════════════════════════════
    public class ZeroIndexToCollapsedConverterTests
    {
        private readonly ZeroIndexToCollapsedConverter _sut = ZeroIndexToCollapsedConverter.Instance;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        [Fact]
        public void Convert_WhenZero_ShouldReturnCollapsed()
        {
            var result = _sut.Convert(0, typeof(Visibility), null, _culture);
            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_WhenNonZero_ShouldReturnVisible()
        {
            var result = _sut.Convert(1, typeof(Visibility), null, _culture);
            result.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void Convert_WhenNull_ShouldReturnCollapsed()
        {
            var result = _sut.Convert(null, typeof(Visibility), null, _culture);
            result.Should().Be(Visibility.Collapsed);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  ColorToBrushConverter 테스트 — SolidColorBrush 생성 = STA 필요
    // ══════════════════════════════════════════════════════════════
    public class ColorToBrushConverterTests
    {
        private readonly ColorToBrushConverter _sut = ColorToBrushConverter.Instance;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        [StaFact]
        public void Convert_WhenColorValue_ShouldReturnFrozenSolidColorBrush()
        {
            // Arrange
            var color = Colors.Red;

            // Act
            var result = _sut.Convert(color, typeof(SolidColorBrush), null, _culture);

            // Assert
            var brush = result.Should().BeOfType<SolidColorBrush>().Subject;
            brush.Color.Should().Be(Colors.Red);
            brush.IsFrozen.Should().BeTrue("변환기는 Freeze 된 brush 를 반환해야 한다");
        }

        [StaFact]
        public void Convert_WhenNonColorValue_ShouldReturnTransparentBrush()
        {
            // Arrange / Act
            var result = _sut.Convert("not a color", typeof(SolidColorBrush), null, _culture);

            // Assert
            result.Should().BeSameAs(Brushes.Transparent);
        }

        [StaFact]
        public void Convert_WhenNull_ShouldReturnTransparentBrush()
        {
            // Arrange / Act
            var result = _sut.Convert(null, typeof(SolidColorBrush), null, _culture);

            // Assert
            result.Should().BeSameAs(Brushes.Transparent);
        }

        [StaFact]
        public void ConvertBack_ShouldThrowNotSupportedException()
        {
            // Arrange / Act
            Action act = () => _sut.ConvertBack(Brushes.Red, typeof(Color), null, _culture);

            // Assert
            act.Should().Throw<NotSupportedException>();
        }
    }
}
