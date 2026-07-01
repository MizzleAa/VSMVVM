using System;
using System.Windows;
using FluentAssertions;
using VSMVVM.WPF.Services;
using Xunit;

namespace VSMVVM.WPF.Tests.Services
{
    public class ZoomServiceTests
    {
        private static ZoomService Create() => new ZoomService();

        // ── 초기 상태 ────────────────────────────────────────────────

        [Fact]
        public void Initial_ZoomLevel_ShouldBeOne()
        {
            // Arrange / Act
            var svc = Create();

            // Assert
            svc.ZoomLevel.Should().Be(1.0);
        }

        [Fact]
        public void Initial_Offsets_ShouldBeZero()
        {
            // Arrange / Act
            var svc = Create();

            // Assert
            svc.OffsetX.Should().Be(0.0);
            svc.OffsetY.Should().Be(0.0);
        }

        // ── ViewChanged 발화 조건 ─────────────────────────────────────

        [Fact]
        public void ZoomLevel_WhenChangedToNewValue_ShouldFireViewChanged()
        {
            // Arrange
            var svc = Create();
            int count = 0;
            svc.ViewChanged += (_, _) => count++;

            // Act
            svc.ZoomLevel = 2.0;

            // Assert
            count.Should().Be(1);
        }

        [Fact]
        public void ZoomLevel_WhenSetToSameValue_ShouldNotFireViewChanged()
        {
            // Arrange
            var svc = Create();
            int count = 0;
            svc.ViewChanged += (_, _) => count++;

            // Act
            svc.ZoomLevel = 1.0; // 초기값과 동일

            // Assert
            count.Should().Be(0, "값이 변하지 않으면 이벤트를 발화하지 않아야 한다");
        }

        [Fact]
        public void OffsetX_WhenChangedToNewValue_ShouldFireViewChanged()
        {
            // Arrange
            var svc = Create();
            int count = 0;
            svc.ViewChanged += (_, _) => count++;

            // Act
            svc.OffsetX = 100.0;

            // Assert
            count.Should().Be(1);
        }

        [Fact]
        public void OffsetX_WhenSetToSameValue_ShouldNotFireViewChanged()
        {
            // Arrange
            var svc = Create();
            int count = 0;
            svc.ViewChanged += (_, _) => count++;

            // Act
            svc.OffsetX = 0.0;

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public void OffsetY_WhenChangedToNewValue_ShouldFireViewChanged()
        {
            // Arrange
            var svc = Create();
            int count = 0;
            svc.ViewChanged += (_, _) => count++;

            // Act
            svc.OffsetY = 50.0;

            // Assert
            count.Should().Be(1);
        }

        // ── ResetView ─────────────────────────────────────────────────

        [Fact]
        public void ResetView_WhenCalled_ShouldRestoreDefaultValues()
        {
            // Arrange
            var svc = Create();
            svc.ZoomLevel = 3.0;
            svc.OffsetX = 200.0;
            svc.OffsetY = 100.0;

            // Act
            svc.ResetView();

            // Assert
            svc.ZoomLevel.Should().Be(1.0);
            svc.OffsetX.Should().Be(0.0);
            svc.OffsetY.Should().Be(0.0);
        }

        [Fact]
        public void ResetView_WhenCalled_ShouldFireViewChangedExactlyOnce()
        {
            // Arrange
            var svc = Create();
            svc.ZoomLevel = 2.0;
            svc.OffsetX = 50.0;
            svc.OffsetY = 50.0;
            int count = 0;
            svc.ViewChanged += (_, _) => count++;

            // Act
            svc.ResetView();

            // Assert
            count.Should().Be(1, "ResetView 는 세 필드를 변경해도 이벤트를 한 번만 발화해야 한다");
        }

        [Fact]
        public void ResetView_WhenAlreadyDefault_ShouldStillFireViewChangedOnce()
        {
            // Arrange — 이미 기본값 상태
            var svc = Create();
            int count = 0;
            svc.ViewChanged += (_, _) => count++;

            // Act
            svc.ResetView();

            // Assert — _suppressNotify 해제 후 항상 RaiseViewChanged() 호출됨
            count.Should().Be(1);
        }

        // ── 좌표 변환 ─────────────────────────────────────────────────

        [Fact]
        public void ScreenToCanvas_WhenZoom2OffsetZero_ShouldHalveCoordinates()
        {
            // Arrange
            var svc = Create();
            svc.ZoomLevel = 2.0;

            // Act
            var result = svc.ScreenToCanvas(new Point(100, 200));

            // Assert
            result.X.Should().BeApproximately(50.0, 1e-9);
            result.Y.Should().BeApproximately(100.0, 1e-9);
        }

        [Fact]
        public void ScreenToCanvas_WhenOffsetApplied_ShouldSubtractOffset()
        {
            // Arrange
            var svc = Create();
            svc.ZoomLevel = 1.0;
            svc.OffsetX = 30.0;
            svc.OffsetY = 40.0;

            // Act — 공식: (screenX - offsetX) / zoom
            var result = svc.ScreenToCanvas(new Point(130, 240));

            // Assert
            result.X.Should().BeApproximately(100.0, 1e-9);
            result.Y.Should().BeApproximately(200.0, 1e-9);
        }

        [Fact]
        public void CanvasToScreen_WhenZoom2OffsetZero_ShouldDoubleCoordinates()
        {
            // Arrange
            var svc = Create();
            svc.ZoomLevel = 2.0;

            // Act
            var result = svc.CanvasToScreen(new Point(50, 100));

            // Assert
            result.X.Should().BeApproximately(100.0, 1e-9);
            result.Y.Should().BeApproximately(200.0, 1e-9);
        }

        [Fact]
        public void CanvasToScreen_WhenOffsetApplied_ShouldAddOffset()
        {
            // Arrange
            var svc = Create();
            svc.ZoomLevel = 1.0;
            svc.OffsetX = 30.0;
            svc.OffsetY = 40.0;

            // Act — 공식: canvasX * zoom + offsetX
            var result = svc.CanvasToScreen(new Point(100, 200));

            // Assert
            result.X.Should().BeApproximately(130.0, 1e-9);
            result.Y.Should().BeApproximately(240.0, 1e-9);
        }

        [Fact]
        public void ScreenToCanvas_ThenCanvasToScreen_ShouldReturnOriginalPoint()
        {
            // Arrange
            var svc = Create();
            svc.ZoomLevel = 1.5;
            svc.OffsetX = -75.0;
            svc.OffsetY = 120.0;
            var original = new Point(320, 240);

            // Act
            var canvas = svc.ScreenToCanvas(original);
            var roundTrip = svc.CanvasToScreen(canvas);

            // Assert
            roundTrip.X.Should().BeApproximately(original.X, 1e-9);
            roundTrip.Y.Should().BeApproximately(original.Y, 1e-9);
        }

        [Fact]
        public void CanvasToScreen_ThenScreenToCanvas_ShouldReturnOriginalPoint()
        {
            // Arrange
            var svc = Create();
            svc.ZoomLevel = 0.5;
            svc.OffsetX = 200.0;
            svc.OffsetY = -50.0;
            var original = new Point(10, 20);

            // Act
            var screen = svc.CanvasToScreen(original);
            var roundTrip = svc.ScreenToCanvas(screen);

            // Assert
            roundTrip.X.Should().BeApproximately(original.X, 1e-9);
            roundTrip.Y.Should().BeApproximately(original.Y, 1e-9);
        }

        // ── IZoomService 인터페이스 사용 ──────────────────────────────

        [Fact]
        public void Interface_WhenUsedAsIZoomService_ShouldBehaveIdentically()
        {
            // Arrange
            IZoomService svc = Create();
            int count = 0;
            svc.ViewChanged += (_, _) => count++;

            // Act
            svc.ZoomLevel = 2.5;

            // Assert
            count.Should().Be(1);
            svc.ZoomLevel.Should().Be(2.5);
        }
    }
}
