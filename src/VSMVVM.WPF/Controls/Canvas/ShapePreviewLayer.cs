using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VSMVVM.WPF.Controls.Tools;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 도형/폴리곤 도구의 진행 중 preview 를 라벨 색 반투명으로 렌더.
    /// Rectangle / Ellipse / Polygon 도구 3종 지원.
    /// Tool 과 Mask 를 바인딩하면 현재 선택된 라벨 색상 자동 조회.
    /// </summary>
    public class ShapePreviewLayer : FrameworkElement
    {
        public static readonly DependencyProperty ToolProperty =
            DependencyProperty.Register(nameof(Tool), typeof(ICanvasTool), typeof(ShapePreviewLayer),
                new PropertyMetadata(null, OnToolChanged));

        public static readonly DependencyProperty MaskProperty =
            DependencyProperty.Register(nameof(Mask), typeof(MaskLayer), typeof(ShapePreviewLayer),
                new PropertyMetadata(null, (d, _) => ((ShapePreviewLayer)d).InvalidateVisual()));

        public static readonly DependencyProperty StrokeBrushProperty =
            DependencyProperty.Register(nameof(StrokeBrush), typeof(Brush), typeof(ShapePreviewLayer),
                new PropertyMetadata(Brushes.Yellow, (d, _) => ((ShapePreviewLayer)d).InvalidateVisual()));

        public static readonly DependencyProperty MouseTrackElementProperty =
            DependencyProperty.Register(nameof(MouseTrackElement), typeof(FrameworkElement), typeof(ShapePreviewLayer),
                new PropertyMetadata(null, OnMouseTrackElementChanged));

        public FrameworkElement? MouseTrackElement
        {
            get => (FrameworkElement?)GetValue(MouseTrackElementProperty);
            set => SetValue(MouseTrackElementProperty, value);
        }

        // Brush/Eraser hover preview 좌표(마스크 픽셀).
        private Point? _cursorPixel;

        public ICanvasTool? Tool
        {
            get => (ICanvasTool?)GetValue(ToolProperty);
            set => SetValue(ToolProperty, value);
        }

        public MaskLayer? Mask
        {
            get => (MaskLayer?)GetValue(MaskProperty);
            set => SetValue(MaskProperty, value);
        }

        public Brush StrokeBrush { get => (Brush)GetValue(StrokeBrushProperty); set => SetValue(StrokeBrushProperty, value); }

        public ShapePreviewLayer()
        {
            IsHitTestVisible = false;
            SnapsToDevicePixels = true;
        }

        /// <summary>현재 RenderTransform scale(=zoom). preview pen 두께/원 반지름을 화면 기준으로 고정하기 위해 1/z 보정에 사용.</summary>
        private double GetZoom()
        {
            if (RenderTransform is MatrixTransform mt && mt.Matrix.M11 > 0.0001)
                return mt.Matrix.M11;
            return 1.0;
        }

        private static void OnToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (ShapePreviewLayer)d;
            // 이전 Tool 의 PreviewChanged 구독 해제.
            Unsubscribe(self, e.OldValue as ICanvasTool);
            Subscribe(self, e.NewValue as ICanvasTool);
            self.InvalidateVisual();
        }

        private static void OnMouseTrackElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (ShapePreviewLayer)d;
            if (e.OldValue is FrameworkElement oldFe)
            {
                oldFe.RemoveHandler(UIElement.MouseMoveEvent, (MouseEventHandler)self.OnTrackMouseMove);
                oldFe.RemoveHandler(UIElement.MouseLeaveEvent, (MouseEventHandler)self.OnTrackMouseLeave);
            }
            if (e.NewValue is FrameworkElement newFe)
            {
                // handledEventsToo=true: LayeredCanvas 가 드래그 중 Handled=true 로 마킹해도 preview 수신.
                newFe.AddHandler(UIElement.MouseMoveEvent, (MouseEventHandler)self.OnTrackMouseMove, handledEventsToo: true);
                newFe.AddHandler(UIElement.MouseLeaveEvent, (MouseEventHandler)self.OnTrackMouseLeave, handledEventsToo: true);
            }
        }

        private void OnTrackMouseMove(object sender, MouseEventArgs e)
        {
            var m = Mask;
            if (m == null || sender is not FrameworkElement fe) return;
            // 뷰포트 좌표(LayeredCanvas 로컬) → canvas pre-transform 좌표 → MaskLayer 로컬 픽셀.
            // LayeredCanvas 는 자체 RenderTransform 이 없고 자식에 transform 을 brodacast 하므로
            // ScreenToCanvas 로 zoom/pan 역변환 필수.
            var pos = e.GetPosition(fe);
            if (fe is IZoomPanViewport vp)
                pos = vp.ScreenToCanvas(pos);
            var maskLeft = System.Windows.Controls.Canvas.GetLeft(m);
            var maskTop = System.Windows.Controls.Canvas.GetTop(m);
            if (double.IsNaN(maskLeft)) maskLeft = 0;
            if (double.IsNaN(maskTop)) maskTop = 0;
            // MaskLayer 의 Width == MaskWidth (픽셀:DIU 1:1) 가정 — BrushTool.ToMaskPixel 과 동일 기준.
            double displayW = m.ActualWidth > 0 ? m.ActualWidth : m.MaskWidth;
            double displayH = m.ActualHeight > 0 ? m.ActualHeight : m.MaskHeight;
            if (displayW <= 0 || displayH <= 0) return;
            _cursorPixel = new Point(
                (pos.X - maskLeft) * m.MaskWidth / displayW,
                (pos.Y - maskTop) * m.MaskHeight / displayH);
            InvalidateVisual();
        }

        private void OnTrackMouseLeave(object sender, MouseEventArgs e)
        {
            _cursorPixel = null;
            InvalidateVisual();
        }

        private static void Subscribe(ShapePreviewLayer self, ICanvasTool? tool)
        {
            switch (tool)
            {
                case RectangleMaskTool rm: rm.PreviewChanged += self.OnPreviewChanged; break;
                case PolygonMaskTool pm: pm.PreviewChanged += self.OnPreviewChanged; break;
                case MagneticLassoTool ml: ml.PreviewChanged += self.OnPreviewChanged; break;
            }
        }

        private static void Unsubscribe(ShapePreviewLayer self, ICanvasTool? tool)
        {
            switch (tool)
            {
                case RectangleMaskTool rm: rm.PreviewChanged -= self.OnPreviewChanged; break;
                case PolygonMaskTool pm: pm.PreviewChanged -= self.OnPreviewChanged; break;
                case MagneticLassoTool ml: ml.PreviewChanged -= self.OnPreviewChanged; break;
            }
        }

        private void OnPreviewChanged(object? sender, EventArgs e) => InvalidateVisual();

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var tool = Tool;
            if (tool == null) return;

            var color = GetLabelColor();
            var fillColor = Color.FromArgb(96, color.R, color.G, color.B);
            var fill = new SolidColorBrush(fillColor); fill.Freeze();
            // Zoom 보정: layer 가 zoom 스케일을 상속하므로 두께/반지름 을 1/z 로 줄여 화면에서 고정 크기 유지.
            double z = GetZoom();
            var pen = new Pen(new SolidColorBrush(color), 1.5 / z); pen.Freeze();
            var dashPen = new Pen(new SolidColorBrush(color), 1.0 / z)
                { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
            dashPen.Freeze();
            double anchorR = 3.0 / z;

            switch (tool)
            {
                case EraserTool er:
                    // 지우개는 실제 Erase 가 찍을 픽셀(정수 격자) 집합을 그대로 그림 — 원이 아니라 픽셀 사각형 union.
                    // 격자와 1:1 일치하므로 고배율에서도 "딱딱한" 픽셀 모양이 보임.
                    if (_cursorPixel is Point ep)
                    {
                        var eraserFill = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)); eraserFill.Freeze();
                        var geo = BuildBrushPixelGeometry(ep, er.Radius);
                        if (geo != null) dc.DrawGeometry(eraserFill, null, geo);
                    }
                    break;

                case BrushTool br:
                    // 브러시도 실제 PaintCircle 이 기록할 픽셀 집합을 그대로 재현.
                    if (_cursorPixel is Point bp)
                    {
                        var geo = BuildBrushPixelGeometry(bp, br.Radius);
                        if (geo != null) dc.DrawGeometry(fill, null, geo);
                    }
                    break;

                case RectangleMaskTool rm:
                    if (rm.CurrentRect is Rect rect && rect.Width > 0 && rect.Height > 0)
                    {
                        if (tool.Mode == CanvasToolMode.EllipseMask)
                        {
                            var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
                            dc.DrawEllipse(fill, pen, center, rect.Width / 2, rect.Height / 2);
                        }
                        else
                        {
                            dc.DrawRectangle(fill, pen, rect);
                        }
                    }
                    break;

                case PolygonMaskTool pm:
                    var pts = pm.CurrentPoints;
                    if (pts.Count < 1) return;
                    if (pts.Count >= 3)
                    {
                        var fig = new PathFigure { StartPoint = pts[0], IsClosed = true, IsFilled = true };
                        for (int i = 1; i < pts.Count; i++)
                            fig.Segments.Add(new LineSegment(pts[i], true));
                        var geo = new PathGeometry();
                        geo.Figures.Add(fig);
                        geo.Freeze();
                        dc.DrawGeometry(fill, pen, geo);
                    }
                    else
                    {
                        // 점 2개 이하: 점 또는 선만.
                        for (int i = 0; i + 1 < pts.Count; i++) dc.DrawLine(pen, pts[i], pts[i + 1]);
                        foreach (var p in pts) dc.DrawEllipse(fill, pen, p, anchorR, anchorR);
                    }
                    break;

                case MagneticLassoTool ml:
                    // 확정 경로: solid 실선.
                    var cp = ml.ConfirmedPath;
                    for (int i = 0; i + 1 < cp.Count; i++) dc.DrawLine(pen, cp[i], cp[i + 1]);
                    // preview: dash 점선.
                    var pp = ml.PreviewPath;
                    if (pp != null)
                        for (int i = 0; i + 1 < pp.Count; i++) dc.DrawLine(dashPen, pp[i], pp[i + 1]);
                    // anchor 표시.
                    foreach (var p in cp) dc.DrawEllipse(fill, pen, p, anchorR, anchorR);
                    break;
            }
        }

        // MaskLayer.PaintCircle 과 동일 공식(center floor → 정수 픽셀 (x,y) 에서 dx²+dy² ≤ r²).
        // 실제 칠해질 픽셀들의 [x, x+1)×[y, y+1) 사각형 합집합을 StreamGeometry 로 구성.
        private static Geometry? BuildBrushPixelGeometry(Point center, int radius)
        {
            if (radius < 0) return null;
            int cx = (int)center.X, cy = (int)center.Y;
            int r2 = radius * radius;
            var geo = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (var ctx = geo.Open())
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int y = cy + dy;
                    int dy2 = dy * dy;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dy2 > r2) continue;
                        int x = cx + dx;
                        ctx.BeginFigure(new Point(x, y), isFilled: true, isClosed: true);
                        ctx.LineTo(new Point(x + 1, y), false, false);
                        ctx.LineTo(new Point(x + 1, y + 1), false, false);
                        ctx.LineTo(new Point(x, y + 1), false, false);
                    }
                }
            }
            geo.Freeze();
            return geo;
        }

        private Color GetLabelColor()
        {
            var m = Mask;
            if (m?.Labels != null)
            {
                var lbl = m.Labels.GetByIndex(m.CurrentLabelIndex);
                if (lbl != null) return lbl.Color;
            }
            // fallback: StrokeBrush 색.
            if (StrokeBrush is SolidColorBrush scb) return scb.Color;
            return Colors.Yellow;
        }
    }
}
