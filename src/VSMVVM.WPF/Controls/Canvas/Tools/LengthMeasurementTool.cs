using System;
using System.Windows;
using System.Windows.Input;
using VSMVVM.WPF.Imaging.Measurements;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>두 점 클릭으로 길이를 측정. MouseDown=시작점 저장, MouseMove=preview 갱신, MouseUp=확정.</summary>
    public class LengthMeasurementTool : CanvasToolBase
    {
        public override CanvasToolMode Mode => CanvasToolMode.LengthMeasurement;
        public override Cursor ToolCursor => Cursors.Pen;

        /// <summary>측정 결과가 저장될 컬렉션. ViewModel 이 주입.</summary>
        public MeasurementCollection? Target { get; set; }

        /// <summary>측정 확정 시 발화. VM 이 Undo 스택에 push 한다.</summary>
        public event EventHandler<LengthMeasurement>? MeasurementCommitted;

        private LengthMeasurement? _preview;

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown || Target == null) return false;
            var p = BrushTool.ToMaskPixel(ctx, position);
            _preview = new LengthMeasurement { Id = Target.NextId(), Start = p, End = p };
            Target.Add(_preview);
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e)
        {
            if (_preview == null) return;
            _preview.End = BrushTool.ToMaskPixel(ctx, position);
        }

        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (_preview == null) return;
            _preview.End = BrushTool.ToMaskPixel(ctx, position);
            // 너무 짧으면 제거 — undo 에도 남기지 않는다.
            if (_preview.Value < 1.0 && Target != null)
            {
                Target.Remove(_preview);
                _preview = null;
                ctx.NotifyDrawingCompleted();
                return;
            }
            var committed = _preview;
            _preview = null;
            MeasurementCommitted?.Invoke(this, committed);
            ctx.NotifyDrawingCompleted();
        }
    }
}
