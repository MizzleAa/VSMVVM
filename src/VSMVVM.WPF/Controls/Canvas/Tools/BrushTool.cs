using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 픽셀 마스크에 원형 브러시로 라벨 인덱스를 채우는 도구.
    /// <see cref="CanvasToolContext.TargetMaskLayer"/>가 없으면 no-op.
    /// </summary>
    public class BrushTool : CanvasToolBase, IMaskMutatingTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.Brush;
        public override Cursor ToolCursor => Cursors.Cross;

        private int _radius = 6;
        private bool _isDrawing;
        private Point? _lastPoint;

        /// <summary>브러시 반지름(픽셀).</summary>
        public int Radius
        {
            get => _radius;
            set { _radius = Math.Max(0, value); OnPropertyChanged(); }
        }

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown) return false;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return false;

            var label = ResolveLabelIndex(ctx);
            BeginLifecycle(mask, label);

            var p = ToMaskPixel(ctx, position);
            mask.PaintCircle(p, _radius, label);
            _isDrawing = true;
            _lastPoint = p;
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e)
        {
            if (!_isDrawing) return;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return;

            var p = ToMaskPixel(ctx, position);
            int label = ResolveLabelIndex(ctx);

            // 포인트 사이를 선형 보간으로 채워 끊김 방지
            if (_lastPoint is Point last)
            {
                var dx = p.X - last.X;
                var dy = p.Y - last.Y;
                var dist = Math.Sqrt(dx * dx + dy * dy);
                var step = Math.Max(1.0, _radius / 2.0);
                var count = Math.Max(1, (int)Math.Ceiling(dist / step));
                for (int i = 1; i <= count; i++)
                {
                    var t = (double)i / count;
                    mask.PaintCircle(new Point(last.X + dx * t, last.Y + dy * t), _radius, label);
                }
            }
            else
            {
                mask.PaintCircle(p, _radius, label);
            }
            _lastPoint = p;
        }

        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            _isDrawing = false;
            _lastPoint = null;
            var mask = ctx.TargetMaskLayer;
            if (mask != null)
                EndLifecycle(mask, ResolveLabelIndex(ctx));
            ctx.NotifyDrawingCompleted();
        }

        /// <summary>현재 툴이 칠할 라벨 인덱스. BrushTool은 MaskLayer.CurrentLabelIndex를 따른다.</summary>
        protected virtual int ResolveLabelIndex(CanvasToolContext ctx)
            => ctx.TargetMaskLayer?.CurrentLabelIndex ?? 1;

        /// <summary>
        /// 스트로크 시작 훅. BrushTool 은 BeginStroke 로 새 인스턴스 ID 발번.
        /// EraserTool 이 오버라이드해 BeginErase 로 분기한다.
        /// </summary>
        protected virtual void BeginLifecycle(VSMVVM.WPF.Controls.MaskLayer mask, int labelIndex)
            => mask.BeginStroke(labelIndex);

        /// <summary>스트로크 종료 훅. BrushTool 은 EndStroke 로 merge/split 판정.</summary>
        protected virtual void EndLifecycle(VSMVVM.WPF.Controls.MaskLayer mask, int labelIndex)
            => mask.EndStroke(labelIndex);

        /// <summary>
        /// LayeredCanvas 로컬 좌표(=position)를 MaskLayer 내부 픽셀 좌표로 변환.
        /// </summary>
        internal static Point ToMaskPixel(CanvasToolContext ctx, Point layeredCanvasPos)
        {
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return layeredCanvasPos;

            var maskLeft = Canvas.GetLeft(mask);
            var maskTop = Canvas.GetTop(mask);
            if (double.IsNaN(maskLeft)) maskLeft = 0;
            if (double.IsNaN(maskTop)) maskTop = 0;

            var localX = layeredCanvasPos.X - maskLeft;
            var localY = layeredCanvasPos.Y - maskTop;

            var displayW = mask.ActualWidth > 0 ? mask.ActualWidth : mask.Width;
            var displayH = mask.ActualHeight > 0 ? mask.ActualHeight : mask.Height;
            if (double.IsNaN(displayW) || displayW <= 0) displayW = mask.MaskWidth;
            if (double.IsNaN(displayH) || displayH <= 0) displayH = mask.MaskHeight;

            var scaleX = mask.MaskWidth / displayW;
            var scaleY = mask.MaskHeight / displayH;
            return new Point(localX * scaleX, localY * scaleY);
        }
    }
}
