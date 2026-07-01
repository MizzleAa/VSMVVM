using System.Windows;
using FluentAssertions;
using VSMVVM.WPF.Imaging;
using Xunit;

namespace VSMVVM.WPF.Tests.Imaging
{
    /// <summary>
    /// LiveWire.Backtrack 경계 가드 회귀 테스트.
    /// 자석 올가미 도구(MagneticLassoTool) 클릭이 이미지 밖일 때 IndexOutOfRangeException 으로 앱이 죽던 버그 가드.
    /// </summary>
    public class LiveWireTests
    {
        // 4×4 raster 의 prev 배열. 모두 self-loop (idx == prev[idx]) → Backtrack 이 시작점에서 즉시 종료.
        // 단위 테스트는 경로 정확성이 아니라 "경계 밖 입력에도 안 죽는다" 만 검증.
        private static int[] MakeSelfLoopPrev(int width, int height)
        {
            var prev = new int[width * height];
            for (int i = 0; i < prev.Length; i++) prev[i] = i;
            return prev;
        }

        [Fact]
        public void Backtrack_ValidDst_ReturnsPath()
        {
            const int w = 4, h = 4;
            var prev = MakeSelfLoopPrev(w, h);

            var path = LiveWire.Backtrack(prev, w, dstX: 2, dstY: 2);

            path.Should().NotBeNull();
            path.Count.Should().Be(1, "self-loop prev 라 시작점만 추가되고 종료");
            path[0].Should().Be(new Point(2, 2));
        }

        [Fact]
        public void Backtrack_DstOutOfRange_X_ReturnsEmpty()
        {
            const int w = 4, h = 4;
            var prev = MakeSelfLoopPrev(w, h);

            var path = LiveWire.Backtrack(prev, w, dstX: 10, dstY: 2);

            path.Should().NotBeNull();
            path.Count.Should().Be(0, "x 가 width 초과면 IndexOutOfRange 대신 빈 경로 반환");
        }

        [Fact]
        public void Backtrack_DstOutOfRange_Y_ReturnsEmpty()
        {
            const int w = 4, h = 4;
            var prev = MakeSelfLoopPrev(w, h);

            var path = LiveWire.Backtrack(prev, w, dstX: 2, dstY: 10);

            path.Count.Should().Be(0, "y 가 height 초과면 IndexOutOfRange 대신 빈 경로 반환");
        }

        [Fact]
        public void Backtrack_DstNegative_ReturnsEmpty()
        {
            const int w = 4, h = 4;
            var prev = MakeSelfLoopPrev(w, h);

            var pathNegX = LiveWire.Backtrack(prev, w, dstX: -1, dstY: 2);
            var pathNegY = LiveWire.Backtrack(prev, w, dstX: 2, dstY: -1);

            pathNegX.Count.Should().Be(0, "음수 x 도 경계 밖 → 빈 경로");
            pathNegY.Count.Should().Be(0, "음수 y 도 경계 밖 → 빈 경로");
        }

        [Fact]
        public void Backtrack_NullPrev_ReturnsEmpty()
        {
            var path = LiveWire.Backtrack(prev: null!, width: 4, dstX: 0, dstY: 0);
            path.Count.Should().Be(0);
        }

        [Fact]
        public void Backtrack_EmptyPrev_ReturnsEmpty()
        {
            var path = LiveWire.Backtrack(prev: System.Array.Empty<int>(), width: 4, dstX: 0, dstY: 0);
            path.Count.Should().Be(0);
        }

        [Fact]
        public void Backtrack_InvalidWidth_ReturnsEmpty()
        {
            var prev = new int[16];
            var path = LiveWire.Backtrack(prev, width: 0, dstX: 0, dstY: 0);
            path.Count.Should().Be(0);
        }
    }
}
