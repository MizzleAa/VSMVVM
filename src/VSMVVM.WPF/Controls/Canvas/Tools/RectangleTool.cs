using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 직사각형 그리기 도구. 기본 자유 비율, Shift=정사각형.
    /// </summary>
    public class RectangleTool : CanvasToolBase
    {
        public override CanvasToolMode Mode => CanvasToolMode.Rectangle;
        public override Cursor ToolCursor => Cursors.Cross;

        private Point _startPoint;
        private Rectangle? _preview;
        private bool _isDrawing;

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown) return false;
            if (ctx.TargetLayer == null) return false;

            _startPoint = GetLayerLocalPosition(ctx, position);
            _preview = new Rectangle
            {
                Stroke = StrokeColor.Clone(),
                StrokeThickness = StrokeThickness,
                Fill = GetPreviewFillBrush(),
                StrokeDashArray = { 4, 2 },
                Width = 0,
                Height = 0,
            };

            Canvas.SetLeft(_preview, _startPoint.X);
            Canvas.SetTop(_preview, _startPoint.Y);
            ctx.TargetLayer.Children.Add(_preview);
            _isDrawing = true;
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e)
        {
            if (!_isDrawing || _preview == null || ctx.TargetLayer == null) return;

            var current = GetLayerLocalPosition(ctx, position);
            var bounds = CalculateBounds(_startPoint, current, ctx.IsShiftDown);

            Canvas.SetLeft(_preview, bounds.X);
            Canvas.SetTop(_preview, bounds.Y);
            _preview.Width = bounds.Width;
            _preview.Height = bounds.Height;
        }

        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (!_isDrawing || _preview == null || ctx.TargetLayer == null) return;

            _isDrawing = false;

            // 미리보기 제거
            ctx.TargetLayer.Children.Remove(_preview);

            var current = GetLayerLocalPosition(ctx, position);
            var bounds = CalculateBounds(_startPoint, current, ctx.IsShiftDown);

            // 너무 작으면 무시
            if (bounds.Width < 3 || bounds.Height < 3)
            {
                _preview = null;
                return;
            }

            // 최종 도형 생성
            var shape = CreateFinalShape(bounds);
            Canvas.SetLeft(shape, bounds.X);
            Canvas.SetTop(shape, bounds.Y);
            ctx.TargetLayer.Children.Add(shape);

            _preview = null;
            ctx.NotifyDrawingCompleted();
        }

        /// <summary>드래그 바운딩 박스 계산. Shift 시 정사각형 보정.</summary>
        protected static Rect CalculateBounds(Point start, Point end, bool constrainSquare)
        {
            var x = Math.Min(start.X, end.X);
            var y = Math.Min(start.Y, end.Y);
            var w = Math.Abs(end.X - start.X);
            var h = Math.Abs(end.Y - start.Y);

            if (constrainSquare)
            {
                var size = Math.Min(w, h);
                w = size;
                h = size;
                // 시작점 기준으로 방향 유지
                x = end.X >= start.X ? start.X : start.X - size;
                y = end.Y >= start.Y ? start.Y : start.Y - size;
            }

            return new Rect(x, y, w, h);
        }

        /// <summary>최종 도형을 생성합니다. 서브클래스에서 오버라이드합니다.</summary>
        protected virtual Shape CreateFinalShape(Rect bounds)
        {
            return new Rectangle
            {
                Width = bounds.Width,
                Height = bounds.Height,
                Stroke = StrokeColor.Clone(),
                StrokeThickness = StrokeThickness,
                Fill = GetFillBrush(),
            };
        }

        protected static Point GetLayerLocalPosition(CanvasToolContext ctx, Point canvasPosition)
        {
            if (ctx.TargetLayer == null) return canvasPosition;
            var layerLeft = Canvas.GetLeft(ctx.TargetLayer);
            var layerTop = Canvas.GetTop(ctx.TargetLayer);
            if (double.IsNaN(layerLeft)) layerLeft = 0;
            if (double.IsNaN(layerTop)) layerTop = 0;
            return new Point(canvasPosition.X - layerLeft, canvasPosition.Y - layerTop);
        }
    }
}
