using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace VSMVVM.WPF.Scheduler.Editor.Renderers
{
    /// <summary>
    /// 좌측 마진. 클릭한 줄에 브레이크포인트를 토글하고 해당 줄에 빨간 원을 그린다.
    /// 외부에서 Breakpoints 집합을 갱신하면 InvalidateVisual을 호출하여 다시 그림.
    /// </summary>
    public sealed class BreakpointMargin : AbstractMargin
    {
        public const double DefaultWidth = 16.0;

        private static readonly Brush FallbackErrorBrush = MakeFrozen("#EF4444"); // Red500

        private readonly HashSet<int> _breakpoints = new();
        private readonly Func<Brush> _brushProvider;

        public BreakpointMargin(Func<Brush> brushProvider = null)
        {
            _brushProvider = brushProvider ?? (() => FallbackErrorBrush);
            Cursor = Cursors.Hand;
        }

        /// <summary>현재 보유한 브레이크포인트 줄 번호(1-based)의 스냅샷.</summary>
        public IReadOnlyCollection<int> Breakpoints => _breakpoints;

        /// <summary>사용자가 마진을 클릭해 줄이 토글될 때 발화. (lineNumber, isSet).</summary>
        public event EventHandler<BreakpointToggledEventArgs> BreakpointToggled;

        /// <summary>외부에서 일괄 설정. UI 갱신 포함.</summary>
        public void SetBreakpoints(IEnumerable<int> lineNumbers)
        {
            _breakpoints.Clear();
            if (lineNumbers != null)
            {
                foreach (var n in lineNumbers)
                {
                    if (n > 0) _breakpoints.Add(n);
                }
            }
            InvalidateVisual();
        }

        public bool ToggleBreakpoint(int lineNumber)
        {
            if (lineNumber <= 0) return false;
            bool isSet;
            if (!_breakpoints.Add(lineNumber))
            {
                _breakpoints.Remove(lineNumber);
                isSet = false;
            }
            else
            {
                isSet = true;
            }
            InvalidateVisual();
            BreakpointToggled?.Invoke(this, new BreakpointToggledEventArgs(lineNumber, isSet));
            return isSet;
        }

        protected override Size MeasureOverride(Size availableSize) => new(DefaultWidth, 0);

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var pos = e.GetPosition(this);
            var tv = TextView;
            if (tv == null || tv.Document == null) return;

            // 마우스 Y → 시각 라인 → 문서 라인 번호
            var line = tv.GetVisualLineFromVisualTop(pos.Y + tv.VerticalOffset);
            if (line == null) return;
            ToggleBreakpoint(line.FirstDocumentLine.LineNumber);
            e.Handled = true;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var tv = TextView;
            if (tv == null || tv.Document == null) return;
            if (_breakpoints.Count == 0) return;

            var brush = _brushProvider() ?? FallbackErrorBrush;
            const double radius = 5.0;

            foreach (var line in tv.VisualLines)
            {
                var docLine = line.FirstDocumentLine.LineNumber;
                if (!_breakpoints.Contains(docLine)) continue;

                var centerY = line.VisualTop + line.Height / 2 - tv.VerticalOffset;
                var centerX = ActualWidth / 2;
                drawingContext.DrawEllipse(brush, null, new Point(centerX, centerY), radius, radius);
            }
        }

        private static Brush MakeFrozen(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>BreakpointMargin.BreakpointToggled 이벤트 인자.</summary>
    public sealed class BreakpointToggledEventArgs : EventArgs
    {
        public int LineNumber { get; }
        public bool IsSet { get; }

        public BreakpointToggledEventArgs(int lineNumber, bool isSet)
        {
            LineNumber = lineNumber;
            IsSet = isSet;
        }
    }
}
