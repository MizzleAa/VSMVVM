using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// SAM-style point-prompt 채우기. 좌클릭 = positive(allow), Shift+좌클릭 = negative(block).
    /// 매 클릭마다 tentative re-fill 로 즉시 결과 표시. Enter / 더블클릭 = commit, Esc = cancel.
    /// </summary>
    public class PointPromptFillTool : CanvasToolBase, IInteractiveCanvasTool, IMaskMutatingTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.PointPromptFill;
        public override Cursor ToolCursor => Cursors.Cross;

        private readonly List<Point> _positive = new();
        private readonly List<Point> _negative = new();
        private bool _active;

        public bool IsInputSessionActive => _active;
        public IReadOnlyList<Point> PositivePoints => _positive;
        public IReadOnlyList<Point> NegativePoints => _negative;

        private int _tolerance = 32;
        /// <summary>0-255 범위 RGB 허용 오차. 기본 32.</summary>
        public int Tolerance
        {
            get => _tolerance;
            set { _tolerance = Math.Max(0, Math.Min(255, value)); OnPropertyChanged(); }
        }

        public event EventHandler? PreviewChanged;
        private void Raise() => PreviewChanged?.Invoke(this, EventArgs.Empty);

        public override bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            // ctx.IsShiftDown / IsCtrlDown 은 stroke 세션 동안 캐시된 값 — 두 번째 클릭부터 stale.
            // LayeredCanvas 가 IsInputSessionActive=true 인 동안 ctx 를 재사용하기 때문.
            // modifier 별 분기를 정확히 하기 위해 매번 Keyboard.IsKeyDown 으로 live sample.
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) return false;

            var mask = ctx.TargetMaskLayer;
            if (mask == null) return false;
            int label = mask.CurrentLabelIndex;
            if (label == 0) return false;

            var p = BrushTool.ToMaskPixel(ctx, position);
            if (!_active)
            {
                _active = true;
                _positive.Clear();
                _negative.Clear();
            }

            bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            if (shift) _negative.Add(p);
            else _positive.Add(p);

            ApplyTentative(mask, label);
            Raise();
            return true;
        }

        // 매 클릭마다 stroke reset 후 처음부터 다시 채우기 — 단순/안전한 incremental 시뮬레이션.
        private void ApplyTentative(MaskLayer mask, int label)
        {
            mask.CancelStroke();
            mask.BeginStroke(label);
            if (_positive.Count == 0) return;
            var pos = _positive.Select(pt => ((int)pt.X, (int)pt.Y)).ToList();
            var neg = _negative.Select(pt => ((int)pt.X, (int)pt.Y)).ToList();
            mask.PaintPointPromptFill(pos, neg, label, _tolerance);
        }

        public bool OnEnterPressed(CanvasToolContext ctx) => Commit(ctx);
        public bool OnDoubleClick(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) => Commit(ctx);

        private bool Commit(CanvasToolContext ctx)
        {
            if (!_active) return false;
            var mask = ctx.TargetMaskLayer;
            if (mask == null) { Reset(); Raise(); return false; }

            int label = mask.CurrentLabelIndex;
            if (_positive.Count > 0) mask.EndStroke(label);
            else mask.CancelStroke();
            Reset();
            Raise();
            ctx.NotifyDrawingCompleted();
            return true;
        }

        public bool OnEscapePressed(CanvasToolContext ctx)
        {
            if (!_active) return false;
            ctx.TargetMaskLayer?.CancelStroke();
            Reset();
            Raise();
            return true;
        }

        private void Reset()
        {
            _positive.Clear();
            _negative.Clear();
            _active = false;
        }

        public override void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e) { }
        public override void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) { }
    }
}
