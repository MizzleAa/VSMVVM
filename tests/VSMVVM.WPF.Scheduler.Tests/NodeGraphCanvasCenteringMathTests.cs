using FluentAssertions;
using VSMVVM.WPF.Scheduler.Controls;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests
{
    /// <summary>
    /// 디버그 흐름(BP/Continue/StepOver)에서 NodeGraphCanvas 가 현재 정지 노드를 자동으로 화면 중앙으로 이동.
    /// 본 테스트는 그 핵심 수학 헬퍼 — node 좌표 + viewport 크기 + zoom 으로 translate offset 계산.
    /// </summary>
    public class NodeGraphCanvasCenteringMathTests
    {
        // 노드의 추정 폭/높이 — NodeGraphCanvas 의 NodeApproxWidthForBounds/HeightForBounds 와 일치 (현재 160 × 80).
        private const double NodeW = 160;
        private const double NodeH = 80;

        /// <summary>
        /// zoom=1, viewport=800×600, node 좌상단 (0,0) → node 중심 (80, 40) 을 viewport 중심 (400, 300) 으로 보내려면
        /// translate = (400-80, 300-40) = (320, 260).
        /// </summary>
        [Fact]
        public void Centering_AtOrigin_Zoom1()
        {
            var (ox, oy) = NodeGraphCanvas.ComputeCenteringOffset(
                nodeX: 0, nodeY: 0,
                nodeWidth: NodeW, nodeHeight: NodeH,
                viewportWidth: 800, viewportHeight: 600,
                zoom: 1.0);

            ox.Should().Be(320);
            oy.Should().Be(260);
        }

        /// <summary>
        /// zoom=1, viewport=800×600, node 좌상단 (500,300) → node 중심 (580, 340) 를 viewport 중심 (400, 300) 으로:
        /// translate = (400-580, 300-340) = (-180, -40).
        /// </summary>
        [Fact]
        public void Centering_OffOrigin_Zoom1()
        {
            var (ox, oy) = NodeGraphCanvas.ComputeCenteringOffset(
                nodeX: 500, nodeY: 300,
                nodeWidth: NodeW, nodeHeight: NodeH,
                viewportWidth: 800, viewportHeight: 600,
                zoom: 1.0);

            ox.Should().Be(-180);
            oy.Should().Be(-40);
        }

        /// <summary>
        /// zoom=2.0, viewport=800×600, node 좌상단 (100,100) → node 중심 (180,140) 화면에서는 (360,280).
        /// viewport 중심 (400,300) 으로 보내려면 translate = (400 - 360, 300 - 280) = (40, 20).
        /// </summary>
        [Fact]
        public void Centering_OffOrigin_Zoom2()
        {
            var (ox, oy) = NodeGraphCanvas.ComputeCenteringOffset(
                nodeX: 100, nodeY: 100,
                nodeWidth: NodeW, nodeHeight: NodeH,
                viewportWidth: 800, viewportHeight: 600,
                zoom: 2.0);

            ox.Should().Be(40);
            oy.Should().Be(20);
        }

        /// <summary>
        /// zoom=0.5, node 좌상단 (1000, 800) → node 중심 (1080, 840) 화면 (540, 420).
        /// viewport 중심 (400, 300) 으로: translate = (400 - 540, 300 - 420) = (-140, -120).
        /// </summary>
        [Fact]
        public void Centering_FarNode_ZoomedOut()
        {
            var (ox, oy) = NodeGraphCanvas.ComputeCenteringOffset(
                nodeX: 1000, nodeY: 800,
                nodeWidth: NodeW, nodeHeight: NodeH,
                viewportWidth: 800, viewportHeight: 600,
                zoom: 0.5);

            ox.Should().Be(-140);
            oy.Should().Be(-120);
        }

        /// <summary>
        /// 음수 좌표 node 도 정상 동작 — zoom=1, viewport=800×600, node 좌상단 (-200, -100) → 중심 (-120, -60).
        /// viewport 중심 (400, 300) 으로: translate = (400 - (-120), 300 - (-60)) = (520, 360).
        /// </summary>
        [Fact]
        public void Centering_NegativeNodeCoords()
        {
            var (ox, oy) = NodeGraphCanvas.ComputeCenteringOffset(
                nodeX: -200, nodeY: -100,
                nodeWidth: NodeW, nodeHeight: NodeH,
                viewportWidth: 800, viewportHeight: 600,
                zoom: 1.0);

            ox.Should().Be(520);
            oy.Should().Be(360);
        }
    }
}
