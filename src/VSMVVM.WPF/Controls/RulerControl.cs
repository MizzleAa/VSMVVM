using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>눈금자 방향.</summary>
    public enum RulerOrientation { Horizontal, Vertical }

    /// <summary>
    /// 포토샵 스타일 고정 눈금자. <see cref="IZoomPanViewport"/> 와 바인딩해 줌/팬에 맞춰
    /// 픽셀 단위 눈금과 라벨을 그린다. 줌 레벨에 따라 tick 간격이 자동 적응.
    /// 마우스 인디케이터는 <see cref="MouseTrackElement"/> 가 지정되면 해당 요소의
    /// MouseMove 를 구독해 현재 픽셀 좌표를 눈금자에 빨간 선으로 표시.
    /// </summary>
    public class RulerControl : Control
    {
        #region DependencyProperties

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(RulerOrientation), typeof(RulerControl),
                new FrameworkPropertyMetadata(RulerOrientation.Horizontal,
                    FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TargetCanvasProperty =
            DependencyProperty.Register(nameof(TargetCanvas), typeof(IZoomPanViewport), typeof(RulerControl),
                new PropertyMetadata(null, OnTargetCanvasChanged));

        public static readonly DependencyProperty MouseTrackElementProperty =
            DependencyProperty.Register(nameof(MouseTrackElement), typeof(FrameworkElement), typeof(RulerControl),
                new PropertyMetadata(null, OnMouseTrackElementChanged));

        public static readonly DependencyProperty RulerThicknessProperty =
            DependencyProperty.Register(nameof(RulerThickness), typeof(double), typeof(RulerControl),
                new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty TickBrushProperty =
            DependencyProperty.Register(nameof(TickBrush), typeof(Brush), typeof(RulerControl),
                new PropertyMetadata(Brushes.Gray, (d, _) => ((RulerControl)d).InvalidateVisual()));

        public static readonly DependencyProperty LabelBrushProperty =
            DependencyProperty.Register(nameof(LabelBrush), typeof(Brush), typeof(RulerControl),
                new PropertyMetadata(Brushes.LightGray, (d, _) => ((RulerControl)d).InvalidateVisual()));

        public static readonly DependencyProperty MouseIndicatorBrushProperty =
            DependencyProperty.Register(nameof(MouseIndicatorBrush), typeof(Brush), typeof(RulerControl),
                new PropertyMetadata(Brushes.Red, (d, _) => ((RulerControl)d).InvalidateVisual()));

        public RulerOrientation Orientation
        {
            get => (RulerOrientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        /// <summary>추적할 뷰포트(LayeredCanvas 등 IZoomPanViewport 구현체).</summary>
        public IZoomPanViewport? TargetCanvas
        {
            get => (IZoomPanViewport?)GetValue(TargetCanvasProperty);
            set => SetValue(TargetCanvasProperty, value);
        }

        /// <summary>마우스 위치를 받을 요소. 보통 TargetCanvas 와 동일하나 타입이 다르므로 별도 DP.</summary>
        public FrameworkElement? MouseTrackElement
        {
            get => (FrameworkElement?)GetValue(MouseTrackElementProperty);
            set => SetValue(MouseTrackElementProperty, value);
        }

        /// <summary>눈금자 두께 (DIU). Horizontal 이면 높이, Vertical 이면 너비.</summary>
        public double RulerThickness
        {
            get => (double)GetValue(RulerThicknessProperty);
            set => SetValue(RulerThicknessProperty, value);
        }

        public Brush TickBrush { get => (Brush)GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }
        public Brush LabelBrush { get => (Brush)GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
        public Brush MouseIndicatorBrush { get => (Brush)GetValue(MouseIndicatorBrushProperty); set => SetValue(MouseIndicatorBrushProperty, value); }

        #endregion

        #region Fields

        // 마우스 캔버스 픽셀 좌표. null 이면 인디케이터 미표시.
        private Point? _mouseCanvasPos;

        // 적응형 tick 후보 (픽셀 단위).
        private static readonly double[] TickCandidates = { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000 };

        #endregion

        #region Constructor

        static RulerControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RulerControl),
                new FrameworkPropertyMetadata(typeof(RulerControl)));
        }

        public RulerControl()
        {
            Focusable = false;
            SnapsToDevicePixels = true;
            ClipToBounds = true;
        }

        #endregion

        #region Layout

        protected override Size MeasureOverride(Size constraint)
        {
            var t = RulerThickness > 0 ? RulerThickness : 20;
            if (Orientation == RulerOrientation.Horizontal)
            {
                double w = double.IsInfinity(constraint.Width) ? 0 : constraint.Width;
                return new Size(w, t);
            }
            else
            {
                double h = double.IsInfinity(constraint.Height) ? 0 : constraint.Height;
                return new Size(t, h);
            }
        }

        #endregion

        #region Target / MouseTrack subscription

        private static void OnTargetCanvasChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (RulerControl)d;
            if (e.OldValue is IZoomPanViewport oldVp)
            {
                oldVp.ViewportChanged -= self.OnTargetViewportChanged;
                oldVp.SizeChanged -= self.OnTargetSizeChanged;
            }
            if (e.NewValue is IZoomPanViewport newVp)
            {
                newVp.ViewportChanged += self.OnTargetViewportChanged;
                newVp.SizeChanged += self.OnTargetSizeChanged;
            }
            self.InvalidateVisual();
        }

        private void OnTargetViewportChanged(object? sender, EventArgs e) => InvalidateVisual();
        private void OnTargetSizeChanged(object sender, SizeChangedEventArgs e) => InvalidateVisual();

        private static void OnMouseTrackElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (RulerControl)d;
            if (e.OldValue is FrameworkElement oldFe)
            {
                oldFe.RemoveHandler(UIElement.MouseMoveEvent, (MouseEventHandler)self.OnTrackMouseMove);
                oldFe.RemoveHandler(UIElement.MouseLeaveEvent, (MouseEventHandler)self.OnTrackMouseLeave);
            }
            if (e.NewValue is FrameworkElement newFe)
            {
                // handledEventsToo=true: 그리기 도구가 Handled=true 로 마킹해도 ruler 인디케이터 수신.
                newFe.AddHandler(UIElement.MouseMoveEvent, (MouseEventHandler)self.OnTrackMouseMove, handledEventsToo: true);
                newFe.AddHandler(UIElement.MouseLeaveEvent, (MouseEventHandler)self.OnTrackMouseLeave, handledEventsToo: true);
            }
        }

        private void OnTrackMouseMove(object sender, MouseEventArgs e)
        {
            var target = TargetCanvas;
            if (target == null || sender is not FrameworkElement fe) return;
            var pos = e.GetPosition(fe);
            _mouseCanvasPos = target.ScreenToCanvas(pos);
            InvalidateVisual();
        }

        private void OnTrackMouseLeave(object sender, MouseEventArgs e)
        {
            _mouseCanvasPos = null;
            InvalidateVisual();
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 배경.
            var bg = Background;
            if (bg != null) dc.DrawRectangle(bg, null, new Rect(0, 0, w, h));

            var target = TargetCanvas;
            if (target == null) return;
            double zoom = target.ZoomLevel;
            if (!(zoom > 0) || !double.IsFinite(zoom)) return;

            // 화면 보이는 캔버스 범위.
            // screen = canvas*zoom + offset → canvas = (screen - offset) / zoom
            // Horizontal 이면 X 축, Vertical 이면 Y 축 기준.
            bool horiz = Orientation == RulerOrientation.Horizontal;
            double offset = horiz ? GetOffsetX(target) : GetOffsetY(target);
            double viewLen = horiz ? w : h;
            double canvasStart = -offset / zoom;
            double canvasEnd = (viewLen - offset) / zoom;

            // 적응형 tick 간격. 화면상 8px 이상 간격 보장.
            double minorStep = ChooseStep(zoom, minScreenPx: 8);
            double majorStep = ChooseStep(zoom, minScreenPx: 60);

            var tickPen = new Pen(TickBrush, 1); tickPen.Freeze();
            var typeface = new Typeface("Segoe UI");

            double firstMinor = Math.Ceiling(canvasStart / minorStep) * minorStep;
            for (double px = firstMinor; px <= canvasEnd; px += minorStep)
            {
                double screen = px * zoom + offset;
                bool isMajor = Math.Abs(Math.IEEERemainder(px, majorStep)) < 1e-6;
                double tickLen = isMajor ? h * 0.7 : h * 0.35;
                if (!horiz) tickLen = isMajor ? w * 0.7 : w * 0.35;

                if (horiz)
                {
                    dc.DrawLine(tickPen, new Point(screen, h - tickLen), new Point(screen, h));
                    if (isMajor)
                    {
                        var text = new FormattedText(
                            ((int)Math.Round(px)).ToString(CultureInfo.InvariantCulture),
                            CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                            typeface, 10, LabelBrush, 96);
                        dc.DrawText(text, new Point(screen + 2, 1));
                    }
                }
                else
                {
                    dc.DrawLine(tickPen, new Point(w - tickLen, screen), new Point(w, screen));
                    if (isMajor)
                    {
                        var text = new FormattedText(
                            ((int)Math.Round(px)).ToString(CultureInfo.InvariantCulture),
                            CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                            typeface, 10, LabelBrush, 96);
                        // 세로 눈금자: 라벨을 90° 회전해서 좁은 폭에 맞춤. 간단히 좌상단에 그림.
                        dc.DrawText(text, new Point(1, screen + 2));
                    }
                }
            }

            // 마우스 인디케이터.
            if (_mouseCanvasPos is Point mc)
            {
                double mpx = horiz ? mc.X : mc.Y;
                double screen = mpx * zoom + offset;
                var indPen = new Pen(MouseIndicatorBrush, 1); indPen.Freeze();
                if (horiz)
                    dc.DrawLine(indPen, new Point(screen, 0), new Point(screen, h));
                else
                    dc.DrawLine(indPen, new Point(0, screen), new Point(w, screen));
            }
        }

        /// <summary>
        /// 화면상 tick 간격이 <paramref name="minScreenPx"/> 이상이 되는 최소 픽셀-캔버스 step 선택.
        /// step*zoom >= minScreenPx 를 만족하는 TickCandidates 의 최솟값.
        /// </summary>
        private static double ChooseStep(double zoom, double minScreenPx)
        {
            foreach (var c in TickCandidates)
            {
                if (c * zoom >= minScreenPx) return c;
            }
            return TickCandidates[TickCandidates.Length - 1];
        }

        // IZoomPanViewport 는 OffsetX/Y 를 직접 노출하지 않음. LayeredCanvas 실제 구현체에서만 노출되므로
        // reflection / pattern match 로 우회. 여기선 ScreenToCanvas(0,0) 의 역함수로 offset 을 복원.
        // screen = canvas*zoom + offset → screen(0) = canvas(0)*zoom + offset → offset = -canvas(0)*zoom
        // 즉 offset = -ScreenToCanvas((0,0)) * zoom.
        private static double GetOffsetX(IZoomPanViewport vp)
        {
            var origin = vp.ScreenToCanvas(new Point(0, 0));
            return -origin.X * vp.ZoomLevel;
        }

        private static double GetOffsetY(IZoomPanViewport vp)
        {
            var origin = vp.ScreenToCanvas(new Point(0, 0));
            return -origin.Y * vp.ZoomLevel;
        }

        #endregion
    }
}
