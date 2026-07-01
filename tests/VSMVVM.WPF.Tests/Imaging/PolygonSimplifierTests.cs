using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FluentAssertions;
using VSMVVM.WPF.Imaging;
using Xunit;

namespace VSMVVM.WPF.Tests.Imaging
{
    // PolygonSimplifier 는 internal — VSMVVM.WPF.csproj 의 InternalsVisibleTo 로 접근 가능.
    public class PolygonSimplifierTests
    {
        // ── 소형 입력 (n ≤ 3) 처리 ───────────────────────────────────

        [Fact]
        public void SimplifyClosed_WhenThreePoints_ShouldReturnAsIs()
        {
            // Arrange
            var pts = new List<Point> { new(0, 0), new(10, 0), new(5, 10) };

            // Act
            var result = PolygonSimplifier.SimplifyClosed(pts, epsilon: 1.0);

            // Assert
            result.Should().HaveCount(3, "n ≤ 3 이면 단순화 없이 반환");
        }

        [Fact]
        public void SimplifyClosed_WhenTwoPoints_ShouldReturnAsIs()
        {
            // Arrange
            var pts = new List<Point> { new(0, 0), new(10, 0) };

            // Act
            var result = PolygonSimplifier.SimplifyClosed(pts, epsilon: 1.0);

            // Assert
            result.Should().HaveCount(2);
        }

        // ── 직선 상의 점들은 양 끝만 남아야 한다 ──────────────────────

        [Fact]
        public void SimplifyClosed_WhenPointsOnStraightLine_ShouldReduceToTwoEndpoints()
        {
            // Arrange — 6개 점이 모두 직선 (x=0..10, y=0)
            var pts = new List<Point>
            {
                new(0, 0), new(2, 0), new(4, 0), new(6, 0), new(8, 0), new(10, 0)
            };

            // Act
            var result = PolygonSimplifier.SimplifyClosed(pts, epsilon: 1.0);

            // Assert — points[0]=(0,0) 와 anchor(가장 먼 점)=(10,0) 만 keep
            result.Should().HaveCount(2);
            result.Should().Contain(new Point(0, 0));
            result.Should().Contain(new Point(10, 0));
        }

        // ── 정사각형 — 4개 모서리 모두 보존 ──────────────────────────

        [Fact]
        public void SimplifyClosed_WhenSquare_ShouldPreserveAllFourCorners()
        {
            // Arrange — 각 변마다 중간 점 1개씩 추가한 8점 정사각형
            var pts = new List<Point>
            {
                new(0, 0),   // 꼭짓점
                new(5, 0),   // 변 중점
                new(10, 0),  // 꼭짓점
                new(10, 5),  // 변 중점
                new(10, 10), // 꼭짓점
                new(5, 10),  // 변 중점
                new(0, 10),  // 꼭짓점
                new(0, 5),   // 변 중점
            };

            // Act
            var result = PolygonSimplifier.SimplifyClosed(pts, epsilon: 1.0);

            // Assert — 꼭짓점 4개는 수직 거리가 크므로 반드시 보존
            result.Should().Contain(new Point(0, 0));
            result.Should().Contain(new Point(10, 0));
            result.Should().Contain(new Point(10, 10));
            result.Should().Contain(new Point(0, 10));
        }

        // ── 큰 epsilon 은 점을 많이 제거한다 ─────────────────────────

        [Fact]
        public void SimplifyClosed_WhenLargeEpsilon_ShouldProduceFewerPoints()
        {
            // Arrange — 원에 근사한 36점 다각형
            var pts = new List<Point>();
            for (int i = 0; i < 36; i++)
            {
                double angle = i * System.Math.PI * 2 / 36;
                pts.Add(new Point(50 + 40 * System.Math.Cos(angle), 50 + 40 * System.Math.Sin(angle)));
            }

            // Act
            var smallEps = PolygonSimplifier.SimplifyClosed(pts, epsilon: 0.1);
            var largeEps = PolygonSimplifier.SimplifyClosed(pts, epsilon: 20.0);

            // Assert
            largeEps.Count.Should().BeLessThan(smallEps.Count, "큰 epsilon 은 더 많이 단순화한다");
        }

        // ── 결과는 항상 입력의 부분집합 ──────────────────────────────

        [Fact]
        public void SimplifyClosed_ResultShouldBeSubsetOfInput()
        {
            // Arrange
            var pts = new List<Point>
            {
                new(0, 0), new(3, 1), new(5, 0), new(7, 2), new(10, 0),
                new(10, 8), new(5, 10), new(0, 8),
            };

            // Act
            var result = PolygonSimplifier.SimplifyClosed(pts, epsilon: 1.5);

            // Assert — 결과의 모든 점은 입력에도 있어야 한다
            foreach (var pt in result)
                pts.Should().Contain(pt, $"점 {pt} 은 입력에 있어야 한다");
        }

        // ── epsilon = 0 → points[0..n-2] 모두 보존 ──────────────────
        // SimplifyClosed 설계: keep[] 배열에 points[n-1] 은 명시적으로 포함하지 않음.
        // (폐곡선은 마지막 점을 첫 점과 묵시적으로 연결해 닫으므로 points[n-1]은 제외 가능)

        [Fact]
        public void SimplifyClosed_WhenZeroEpsilon_ShouldKeepAllPointsExceptPossiblyLast()
        {
            // Arrange
            var pts = new List<Point>
            {
                new(0, 0), new(5, 1), new(10, 0), new(9, 5), new(10, 10), new(5, 9), new(0, 10), new(1, 5)
            };

            // Act
            var result = PolygonSimplifier.SimplifyClosed(pts, epsilon: 0.0);

            // Assert — epsilon=0 이면 점수는 n-1 이상, 모든 결과 점은 입력의 부분집합
            result.Count.Should().BeGreaterThanOrEqualTo(pts.Count - 1,
                "epsilon=0 이면 interior 점은 모두 keep, points[n-1] 만 제외될 수 있음");
            foreach (var pt in result)
                pts.Should().Contain(pt, $"{pt} 은 입력에 있어야 한다");
        }

        // ── 결정론적 결과 — 같은 입력 두 번 호출 시 동일 결과 ────────

        [Fact]
        public void SimplifyClosed_WhenCalledTwice_ShouldReturnSameResult()
        {
            // Arrange
            var pts = new List<Point>
            {
                new(0, 0), new(5, 1), new(10, 3), new(8, 8), new(3, 10)
            };

            // Act
            var r1 = PolygonSimplifier.SimplifyClosed(pts, epsilon: 2.0);
            var r2 = PolygonSimplifier.SimplifyClosed(pts, epsilon: 2.0);

            // Assert
            r1.Should().BeEquivalentTo(r2, o => o.WithStrictOrdering());
        }
    }
}
