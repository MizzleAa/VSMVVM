using System;
using System.Windows;
using System.Windows.Media;
using VSMVVM.WPF.Controls;
using VSMVVM.WPF.Scheduler.ViewModels;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>
    /// 노드 그래프 전용 미니맵. 기존 MiniMapControl을 상속하여 뷰포트 사각형 그리기 + 클릭 팬은 그대로 사용하고,
    /// OnRender override로 노드 위치를 작은 사각형으로 함께 그린다.
    /// 콘텐츠 영역(ContentWidth/Height)은 노드 바운딩 박스의 union + 10% 패딩으로 자동 계산.
    /// </summary>
    public class NodeGraphMiniMap : MiniMapControl
    {
        public static readonly DependencyProperty GraphProperty =
            DependencyProperty.Register(nameof(Graph), typeof(NodeGraphViewModel), typeof(NodeGraphMiniMap),
                new PropertyMetadata(null, OnGraphChanged));

        public NodeGraphViewModel Graph
        {
            get => (NodeGraphViewModel)GetValue(GraphProperty);
            set => SetValue(GraphProperty, value);
        }

        // 노드 본체 추정 폭/높이 (Phase 7에서 실 측정 값으로 교체).
        public const double NodeApproxWidth = 160;
        public const double NodeApproxHeight = 80;

        public NodeGraphMiniMap()
        {
            // 우리가 직접 노드 사각형을 그리므로 base의 ImageSource는 무시.
            Background = Brushes.Transparent;
            // base.OnRender 가 ImageSource 없을 때 깔던 DimGray fallback 박스(가로 띠 회색)만 제거.
            ContentBackground = Brushes.Transparent;
            // viewport 사각형의 Fill 만 제거 — 테두리(ViewportStroke) 는 base default(노란/Accent) 유지하여
            // 현재 보이는 영역을 미니맵에서 식별할 수 있도록 한다.
            ViewportBrush = Brushes.Transparent;
        }

        private static void OnGraphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var map = (NodeGraphMiniMap)d;
            map.UpdateContentBounds();
            map.InvalidateVisual();
        }

        /// <summary>그래프 변경 시 호출되어야 한다 (Phase 7에서 자동 구독). 현재는 수동 호출.</summary>
        public void RefreshFromGraph()
        {
            UpdateContentBounds();
            InvalidateVisual();
        }

        /// <summary>컨텐츠 좌표계의 폭 (노드 union + 패딩).</summary>
        public double ComputedContentWidth { get; private set; }
        /// <summary>컨텐츠 좌표계의 높이.</summary>
        public double ComputedContentHeight { get; private set; }
        /// <summary>콘텐츠 좌표계의 원점 오프셋 (그래프의 최소 X - 패딩). 노드를 미니맵 좌표로 변환할 때 사용.</summary>
        public double ContentOriginX { get; private set; }
        public double ContentOriginY { get; private set; }

        private void UpdateContentBounds()
        {
            if (Graph == null || Graph.Nodes.Count == 0)
            {
                ComputedContentWidth = 0;
                ComputedContentHeight = 0;
                ContentOriginX = 0;
                ContentOriginY = 0;
                return;
            }

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var n in Graph.Nodes)
            {
                if (n.X < minX) minX = n.X;
                if (n.Y < minY) minY = n.Y;
                if (n.X + NodeApproxWidth > maxX) maxX = n.X + NodeApproxWidth;
                if (n.Y + NodeApproxHeight > maxY) maxY = n.Y + NodeApproxHeight;
            }
            // 10% 패딩
            var padX = (maxX - minX) * 0.1;
            var padY = (maxY - minY) * 0.1;
            ContentOriginX = minX - padX;
            ContentOriginY = minY - padY;
            ComputedContentWidth = (maxX - minX) + padX * 2;
            ComputedContentHeight = (maxY - minY) + padY * 2;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (Graph == null) return;
            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0 || ComputedContentWidth <= 0 || ComputedContentHeight <= 0) return;

            var scale = Math.Min(w / ComputedContentWidth, h / ComputedContentHeight);
            var drawW = ComputedContentWidth * scale;
            var drawH = ComputedContentHeight * scale;
            var offX = (w - drawW) / 2;
            var offY = (h - drawH) / 2;

            var nodeFill = TryFindResource("AccentSecondary") as Brush ?? Brushes.Gray;
            var nodeStroke = TryFindResource("BorderFocus") as Brush ?? Brushes.LightBlue;
            var pen = new Pen(nodeStroke, 0.5);
            pen.Freeze();

            foreach (var n in Graph.Nodes)
            {
                var rx = offX + (n.X - ContentOriginX) * scale;
                var ry = offY + (n.Y - ContentOriginY) * scale;
                var rw = NodeApproxWidth * scale;
                var rh = NodeApproxHeight * scale;
                dc.DrawRectangle(nodeFill, pen, new Rect(rx, ry, rw, rh));
            }
        }
    }
}
