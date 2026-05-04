using FluentAssertions;
using Xunit;

namespace VSMVVM.Core.Tests.Guard
{
    public class GuardTests
    {
        [Fact]
        public void IsNotNull_Null_ThrowsArgumentNullException()
        {
            var act = () => Core.Guard.Guard.IsNotNull(null, "param");
            act.Should().Throw<System.ArgumentNullException>();
        }

        [Fact]
        public void IsNotNull_ValidValue_DoesNotThrow()
        {
            var act = () => Core.Guard.Guard.IsNotNull("hello", "param");
            act.Should().NotThrow();
        }

        [Fact]
        public void IsNotNullOrEmpty_Empty_ThrowsArgumentException()
        {
            var act = () => Core.Guard.Guard.IsNotNullOrEmpty("", "param");
            act.Should().Throw<System.ArgumentException>();
        }

        [Fact]
        public void IsInRange_InRange_DoesNotThrow()
        {
            var act = () => Core.Guard.Guard.IsInRange(5, 1, 10, "param");
            act.Should().NotThrow();
        }

        [Fact]
        public void IsInRange_OutOfRange_ThrowsArgumentOutOfRangeException()
        {
            var act = () => Core.Guard.Guard.IsInRange(15, 1, 10, "param");
            act.Should().Throw<System.ArgumentOutOfRangeException>();
        }

        [Fact]
        public void IsOfType_CorrectType_DoesNotThrow()
        {
            var act = () => Core.Guard.Guard.IsOfType<string>("hello", "param");
            act.Should().NotThrow();
        }

        [Fact]
        public void IsNotEmpty_EmptyCollection_ThrowsArgumentException()
        {
            var act = () => Core.Guard.Guard.IsNotEmpty(new System.Collections.Generic.List<int>(), "param");
            act.Should().Throw<System.ArgumentException>();
        }

        // 회귀 테스트: null reference value를 받았을 때 NRE가 아니라 의미 있는 예외를 던져야 한다.
        [Fact]
        public void IsEqualTo_NullValue_DoesNotThrowNRE()
        {
            var act = () => Core.Guard.Guard.IsEqualTo<string>(null, "expected", "param");
            act.Should().Throw<System.ArgumentException>()
                .And.Should().NotBeOfType<System.NullReferenceException>();
        }

        [Fact]
        public void IsEqualTo_BothNull_DoesNotThrow()
        {
            // EqualityComparer<string>.Default.Equals(null, null) == true 라서 검증 통과.
            var act = () => Core.Guard.Guard.IsEqualTo<string>(null, null, "param");
            act.Should().NotThrow();
        }

        [Fact]
        public void IsGreaterThan_NullValue_ThrowsArgumentNullException()
        {
            var act = () => Core.Guard.Guard.IsGreaterThan<string>(null, "min", "param");
            act.Should().Throw<System.ArgumentNullException>();
        }

        [Fact]
        public void IsInRange_NullValue_ThrowsArgumentNullException()
        {
            var act = () => Core.Guard.Guard.IsInRange<string>(null, "a", "z", "param");
            act.Should().Throw<System.ArgumentNullException>();
        }
    }
}
