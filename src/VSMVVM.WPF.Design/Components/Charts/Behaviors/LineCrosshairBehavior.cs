using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Xaml.Behaviors;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts.Behaviors
{
    /// <summary>
    /// HoverMode=XUnified일 때 Canvas에 vertical line + 시리즈별 dot + 통합 tooltip을 그린다.
    /// </summary>
    public class LineCrosshairBehavior : Behavior<ChartBase>
    {
        public static readonly DependencyProperty CrosshairCanvasProperty =
            DependencyProperty.Register(nameof(CrosshairCanvas), typeof(Canvas), typeof(LineCrosshairBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty TooltipElementProperty =
            DependencyProperty.Register(nameof(TooltipElement), typeof(FrameworkElement), typeof(LineCrosshairBehavior),
                new PropertyMetadata(null, OnTooltipElementChanged));

        public static readonly DependencyProperty TitleElementProperty =
            DependencyProperty.Register(nameof(TitleElement), typeof(TextBlock), typeof(LineCrosshairBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty BodyElementProperty =
            DependencyProperty.Register(nameof(BodyElement), typeof(TextBlock), typeof(LineCrosshairBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty LineBrushProperty =
            DependencyProperty.Register(nameof(LineBrush), typeof(Brush), typeof(LineCrosshairBehavior),
                new PropertyMetadata(Brushes.Gray));

        public static readonly DependencyProperty LineThicknessProperty =
            DependencyProperty.Register(nameof(LineThickness), typeof(double), typeof(LineCrosshairBehavior),
                new PropertyMetadata(1.0));

        public static readonly DependencyProperty DotRadiusProperty =
            DependencyProperty.Register(nameof(DotRadius), typeof(double), typeof(LineCrosshairBehavior),
                new PropertyMetadata(4.0));

        public Canvas CrosshairCanvas { get => (Canvas)GetValue(CrosshairCanvasProperty); set => SetValue(CrosshairCanvasProperty, value); }
        public FrameworkElement TooltipElement { get => (FrameworkElement)GetValue(TooltipElementProperty); set => SetValue(TooltipElementProperty, value); }
        public TextBlock TitleElement { get => (TextBlock)GetValue(TitleElementProperty); set => SetValue(TitleElementProperty, value); }
        public TextBlock BodyElement { get => (TextBlock)GetValue(BodyElementProperty); set => SetValue(BodyElementProperty, value); }
        public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
        public double LineThickness { get => (double)GetValue(LineThicknessProperty); set => SetValue(LineThicknessProperty, value); }
        public double DotRadius { get => (double)GetValue(DotRadiusProperty); set => SetValue(DotRadiusProperty, value); }

        private Line _verticalLine;
        private readonly List<Ellipse> _dotPool = new();
        private TranslateTransform _tooltipTranslate;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.XUnifiedHoverChanged += OnUnifiedHover;
            EnsureCanvasArtifacts();
            EnsureTooltipReady();
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null) AssociatedObject.XUnifiedHoverChanged -= OnUnifiedHover;
            Hide();
            base.OnDetaching();
        }

        private static void OnTooltipElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LineCrosshairBehavior self) self.EnsureTooltipReady();
        }

        private void EnsureCanvasArtifacts()
        {
            var canvas = CrosshairCanvas;
            if (canvas == null) return;
            if (_verticalLine == null)
            {
                _verticalLine = new Line
                {
                    Stroke = LineBrush,
                    StrokeThickness = LineThickness,
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    IsHitTestVisible = false,
                    Visibility = Visibility.Collapsed,
                };
                canvas.Children.Add(_verticalLine);
            }
        }

        private void EnsureTooltipReady()
        {
            var t = TooltipElement;
            if (t == null) return;
            t.Visibility = Visibility.Visible;
            t.Opacity = 0;
            t.IsHitTestVisible = false;
            _tooltipTranslate = t.RenderTransform as TranslateTransform;
            if (_tooltipTranslate == null)
            {
                _tooltipTranslate = new TranslateTransform();
                t.RenderTransform = _tooltipTranslate;
            }
            t.UpdateLayout();
        }

        private void OnUnifiedHover(object sender, XUnifiedHoverState e)
        {
            var canvas = CrosshairCanvas;
            if (canvas == null) return;
            if (e == null || !e.HasValue) { Hide(); return; }

            var chart = AssociatedObject;
            var plotRect = chart.PlotRect;
            var lineX = chart.DataToViewX(e.DataX);

            // Vertical line
            if (_verticalLine != null)
            {
                _verticalLine.X1 = lineX; _verticalLine.X2 = lineX;
                _verticalLine.Y1 = plotRect.Top; _verticalLine.Y2 = plotRect.Bottom;
                _verticalLine.Stroke = LineBrush;
                _verticalLine.StrokeThickness = LineThickness;
                _verticalLine.Visibility = Visibility.Visible;
            }

            // Dots — pool 재사용
            for (var i = 0; i < e.Points.Count; i++)
            {
                Ellipse dot;
                if (i < _dotPool.Count)
                {
                    dot = _dotPool[i];
                }
                else
                {
                    dot = new Ellipse { IsHitTestVisible = false };
                    _dotPool.Add(dot);
                    canvas.Children.Add(dot);
                }
                var p = e.Points[i];
                var r = DotRadius;
                dot.Width = r * 2; dot.Height = r * 2;
                dot.Fill = p.Brush ?? Brushes.White;
                dot.Stroke = Brushes.White;
                dot.StrokeThickness = 1;
                Canvas.SetLeft(dot, p.ScreenPoint.X - r);
                Canvas.SetTop(dot, p.ScreenPoint.Y - r);
                dot.Visibility = Visibility.Visible;
            }
            // 남은 dot 숨김
            for (var i = e.Points.Count; i < _dotPool.Count; i++)
            {
                _dotPool[i].Visibility = Visibility.Collapsed;
            }

            // Tooltip
            if (TooltipElement != null && _tooltipTranslate != null)
            {
                if (TitleElement != null) TitleElement.Text = $"X = {e.DataX:0.###}";
                if (BodyElement != null)
                {
                    var sb = new StringBuilder();
                    foreach (var p in e.Points)
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(p.SeriesTitle ?? "Series");
                        sb.Append(": ");
                        sb.Append(p.DataY.ToString("0.###"));
                    }
                    BodyElement.Text = sb.ToString();
                }
                var size = TooltipElement.RenderSize;
                if (size.Width <= 0 || size.Height <= 0)
                {
                    TooltipElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    size = TooltipElement.DesiredSize;
                }
                var pos = chart.ResolveTooltipPosition(e.ScreenPoint, size, 12);
                _tooltipTranslate.X = pos.X;
                _tooltipTranslate.Y = pos.Y;
                TooltipElement.Opacity = 1;
            }
        }

        private void Hide()
        {
            if (_verticalLine != null) _verticalLine.Visibility = Visibility.Collapsed;
            foreach (var d in _dotPool) d.Visibility = Visibility.Collapsed;
            if (TooltipElement != null) TooltipElement.Opacity = 0;
        }
    }
}
