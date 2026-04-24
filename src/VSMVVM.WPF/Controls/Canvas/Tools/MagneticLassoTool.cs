using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using VSMVVM.WPF.Imaging;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 자석 올가미 (Magnetic Lasso). Live-wire Dijkstra 로 anchor 에서 마우스까지 경로가
    /// 이미지 엣지를 따라 붙음. 클릭으로 anchor 확정, DblClick/Enter 로 닫아 폴리곤 인스턴스.
    /// </summary>
    public class MagneticLassoTool : CanvasToolBase, IInteractiveCanvasTool, IMaskMutatingTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.MagneticLasso;
        public override Cursor ToolCursor => Cursors.Pen;

        // 확정된 경로 (anchor 들 사이 backtrack 결과 누적).
        private readonly List<Point> _confirmedPath = new();
        // 현재 anchor → 마우스까지 backtrack preview.
        private List<Point>? _previewPath;
        private Point? _currentAnchor; // 최근 확정 anchor
        private int[]? _currentPrev;   // 최근 anchor Dijkstra 결과
        private bool _active;

        // Dijkstra 윈도우 (anchor 주변 한 변 크기).
        private const int WINDOW_SIZE = 512;

        // Edge contrast cutoff (0~1). gMax 대비 이 비율 미만의 gradient 는 엣지로 취급 안 함.
        private double _edgeContrast = 0.1;
        /// <summary>약한 엣지 무시 임계값. 0 = 모든 gradient 반영, 값이 커지면 strong edge 만 경로에 붙음.</summary>
        public double EdgeContrast
        {
            get => _edgeContrast;
            set { _edgeContrast = Math.Max(0, Math.Min(1, value)); OnPropertyChanged(); }
        }

        public bool IsInputSessionActive => _active;

        public IReadOnlyList<Point> ConfirmedPath => _confirmedPath;
        public IReadOnlyList<Point>? PreviewPath => _previewPath;

        public event EventHandler? PreviewChanged;
        private void Raise() => PreviewChanged?.Invoke(this, EventArgs.Empty);

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown) return false;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return false;
            var grad = mask.GetSourceGradient();
            if (grad == null) return false; // SourceImage 미설정.

            var p = BrushTool.ToMaskPixel(ctx, position);

            if (!_active)
            {
                mask.BeginStroke(ResolveLabelIndex(ctx));
                _active = true;
                _confirmedPath.Clear();
                _confirmedPath.Add(p);
                _currentAnchor = p;
                _currentPrev = RunDijkstraAround(mask, p);
            }
            else
            {
                // 이전 anchor → 현재 클릭까지 경로를 confirmed 로 flush.
                FlushPreviewToConfirmed(mask, p);
                _currentAnchor = p;
                _currentPrev = RunDijkstraAround(mask, p);
            }
            _previewPath = null;
            Raise();
            return true;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e)
        {
            if (!_active || _currentPrev == null || _currentAnchor == null) return;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) return;
            var p = BrushTool.ToMaskPixel(ctx, position);
            int px = (int)p.X, py = (int)p.Y;
            if ((uint)px >= (uint)mask.MaskWidth || (uint)py >= (uint)mask.MaskHeight) return;

            var path = LiveWire.Backtrack(_currentPrev, mask.MaskWidth, px, py);
            _previewPath = new List<Point>(path);
            Raise();
        }

        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) { }

        public bool OnDoubleClick(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) => Finalize(ctx);
        public bool OnEnterPressed(CanvasToolContext ctx) => Finalize(ctx);

        public bool OnEscapePressed(CanvasToolContext ctx)
        {
            if (!_active) return false;
            ctx.TargetMaskLayer?.CancelStroke();
            Reset();
            Raise();
            return true;
        }

        private bool Finalize(CanvasToolContext ctx)
        {
            var mask = ctx.TargetMaskLayer;
            if (!_active || mask == null) return false;

            // 마지막 anchor → 첫 anchor 로 닫는 경로 추가.
            if (_confirmedPath.Count > 0 && _currentPrev != null)
            {
                var firstP = _confirmedPath[0];
                var closing = LiveWire.Backtrack(_currentPrev, mask.MaskWidth, (int)firstP.X, (int)firstP.Y);
                foreach (var pt in closing) _confirmedPath.Add(pt);
            }

            if (_confirmedPath.Count >= 3)
            {
                int label = ResolveLabelIndex(ctx);
                // Live-wire 경로는 픽셀 해상도로 조밀하므로 vertex 편집용으로 간소화.
                var savedPoints = PolygonMaskTool.Simplify(_confirmedPath, minSpacing: 6.0);
                mask.PaintPolygon(_confirmedPath, label);
                mask.EndStroke(label);
                var inst = mask.Instances.GetById(mask.LastCreatedInstanceId);
                if (inst != null) inst.PolygonPoints = savedPoints;
            }
            else
            {
                mask.CancelStroke();
            }
            Reset();
            Raise();
            ctx.NotifyDrawingCompleted();
            return true;
        }

        private void Reset()
        {
            _confirmedPath.Clear();
            _previewPath = null;
            _currentAnchor = null;
            _currentPrev = null;
            _active = false;
        }

        private void FlushPreviewToConfirmed(MaskLayer mask, Point newAnchor)
        {
            if (_currentPrev == null) return;
            var path = LiveWire.Backtrack(_currentPrev, mask.MaskWidth, (int)newAnchor.X, (int)newAnchor.Y);
            foreach (var pt in path) _confirmedPath.Add(pt);
        }

        private int[]? RunDijkstraAround(MaskLayer mask, Point anchor)
        {
            var grad = mask.GetSourceGradient();
            if (grad == null) return null;
            int half = WINDOW_SIZE / 2;
            var window = new Rect(
                Math.Max(0, anchor.X - half),
                Math.Max(0, anchor.Y - half),
                WINDOW_SIZE, WINDOW_SIZE);
            return LiveWire.RunDijkstra(grad, mask.MaskWidth, mask.MaskHeight,
                (int)anchor.X, (int)anchor.Y, window, _edgeContrast);
        }

        protected virtual int ResolveLabelIndex(CanvasToolContext ctx)
            => ctx.TargetMaskLayer?.CurrentLabelIndex ?? 1;
    }
}
