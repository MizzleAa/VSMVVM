using System;
using System.Windows;
using System.Windows.Input;
using VSMVVM.WPF.Imaging.Measurements;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 3점 클릭으로 각도를 측정. 1=P1, 2=Vertex(꼭지점), 3=P2.
    /// MouseMove 로 다음 점 preview 갱신. Escape 취소.
    /// </summary>
    public class AngleMeasurementTool : CanvasToolBase, IInteractiveCanvasTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.AngleMeasurement;
        public override Cursor ToolCursor => Cursors.Pen;

        public MeasurementCollection? Target { get; set; }

        /// <summary>측정 확정 시 발화. VM 이 Undo 스택에 push 한다.</summary>
        public event EventHandler<AngleMeasurement>? MeasurementCommitted;

        private AngleMeasurement? _preview;
        private int _clicks;

        public bool IsInputSessionActive => _preview != null;

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown || Target == null) return false;
            var p = BrushTool.ToMaskPixel(ctx, position);

            if (_preview == null)
            {
                _preview = new AngleMeasurement { Id = Target.NextId(), P1 = p, Vertex = p, P2 = p };
                Target.Add(_preview);
                _clicks = 1;
            }
            else if (_clicks == 1)
            {
                _preview.Vertex = p;
                _preview.P2 = p;
                _clicks = 2;
            }
            else if (_clicks == 2)
            {
                _preview.P2 = p;
                _clicks = 0;
                var committed = _preview;
                _preview = null; // 완료
                MeasurementCommitted?.Invoke(this, committed);
                ctx.NotifyDrawingCompleted();
            }
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e)
        {
            if (_preview == null) return;
            var p = BrushTool.ToMaskPixel(ctx, position);
            if (_clicks == 1) { _preview.Vertex = p; _preview.P2 = p; }
            else if (_clicks == 2) { _preview.P2 = p; }
        }

        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) { }

        public bool OnEnterPressed(CanvasToolContext ctx)
        {
            if (_preview == null) return false;
            // Enter 는 각도 측정엔 의미 없음(3점 필수). 취소 처리.
            return OnEscapePressed(ctx);
        }

        public bool OnEscapePressed(CanvasToolContext ctx)
        {
            if (_preview == null) return false;
            Target?.Remove(_preview);
            _preview = null;
            _clicks = 0;
            return true;
        }

        public bool OnDoubleClick(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) => false;
    }
}
