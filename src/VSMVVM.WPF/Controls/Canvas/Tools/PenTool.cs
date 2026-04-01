using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 자유 곡선(Polyline) 그리기 도구.
    /// </summary>
    public class PenTool : CanvasToolBase
    {
        public override CanvasToolMode Mode => CanvasToolMode.Pen;
        public override Cursor ToolCursor => Cursors.Cross;

        private Polyline? _currentPolyline;
        private bool _isDrawing;

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown) return false; // Pan 패스스루

            if (ctx.TargetLayer == null) return false;

            _currentPolyline = new Polyline
            {
                Stroke = StrokeColor.Clone(),
                StrokeThickness = StrokeThickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };

            // 레이어 로컬 좌표로 변환
            var layerPos = GetLayerLocalPosition(ctx, position);
            _currentPolyline.Points.Add(layerPos);

            ctx.TargetLayer.Children.Add(_currentPolyline);
            _isDrawing = true;
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e)
        {
            if (!_isDrawing || _currentPolyline == null) return;

            var layerPos = GetLayerLocalPosition(ctx, position);
            _currentPolyline.Points.Add(layerPos);
        }

        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (!_isDrawing || _currentPolyline == null) return;

            _isDrawing = false;

            // Points 정규화: bounding box 기준으로 Canvas.Left/Top 설정
            NormalizePolyline(_currentPolyline);

            _currentPolyline = null;
            ctx.NotifyDrawingCompleted();
        }

        /// <summary>
        /// Polyline의 Points를 정규화합니다.
        /// min(X,Y) → Canvas.Left/Top 으로 이동, Points에서 offset 차감.
        /// 이렇게 해야 Adorner가 실제 그려진 위치를 올바르게 감쌉니다.
        /// </summary>
        private static void NormalizePolyline(Polyline polyline)
        {
            if (polyline.Points.Count == 0) return;

            var minX = double.MaxValue;
            var minY = double.MaxValue;
            foreach (var pt in polyline.Points)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
            }

            // offset 차감한 새 포인트 컬렉션
            var normalized = new PointCollection(polyline.Points.Count);
            foreach (var pt in polyline.Points)
            {
                normalized.Add(new Point(pt.X - minX, pt.Y - minY));
            }
            polyline.Points = normalized;

            // Canvas 위치 설정
            Canvas.SetLeft(polyline, minX);
            Canvas.SetTop(polyline, minY);
        }

        private static Point GetLayerLocalPosition(CanvasToolContext ctx, Point canvasPosition)
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
