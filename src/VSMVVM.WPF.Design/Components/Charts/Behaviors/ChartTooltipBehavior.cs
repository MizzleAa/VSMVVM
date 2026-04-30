using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts.Behaviors
{
    public class ChartTooltipBehavior : Behavior<ChartBase>
    {
        public static readonly DependencyProperty TooltipElementProperty =
            DependencyProperty.Register(nameof(TooltipElement), typeof(FrameworkElement), typeof(ChartTooltipBehavior),
                new PropertyMetadata(null, OnTooltipElementChanged));

        public static readonly DependencyProperty TitleElementProperty =
            DependencyProperty.Register(nameof(TitleElement), typeof(TextBlock), typeof(ChartTooltipBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty BodyElementProperty =
            DependencyProperty.Register(nameof(BodyElement), typeof(TextBlock), typeof(ChartTooltipBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty TitleFormatProperty =
            DependencyProperty.Register(nameof(TitleFormat), typeof(string), typeof(ChartTooltipBehavior),
                new PropertyMetadata("{0}"));

        public static readonly DependencyProperty BodyFormatProperty =
            DependencyProperty.Register(nameof(BodyFormat), typeof(string), typeof(ChartTooltipBehavior),
                new PropertyMetadata("X: {0:0.###}  Y: {1:0.###}"));

        public static readonly DependencyProperty TooltipMarginProperty =
            DependencyProperty.Register(nameof(TooltipMargin), typeof(double), typeof(ChartTooltipBehavior),
                new PropertyMetadata(12.0));

        public FrameworkElement TooltipElement { get => (FrameworkElement)GetValue(TooltipElementProperty); set => SetValue(TooltipElementProperty, value); }
        public TextBlock TitleElement { get => (TextBlock)GetValue(TitleElementProperty); set => SetValue(TitleElementProperty, value); }
        public TextBlock BodyElement { get => (TextBlock)GetValue(BodyElementProperty); set => SetValue(BodyElementProperty, value); }
        public string TitleFormat { get => (string)GetValue(TitleFormatProperty); set => SetValue(TitleFormatProperty, value); }
        public string BodyFormat { get => (string)GetValue(BodyFormatProperty); set => SetValue(BodyFormatProperty, value); }
        public double TooltipMargin { get => (double)GetValue(TooltipMarginProperty); set => SetValue(TooltipMarginProperty, value); }

        private TranslateTransform _translate;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.HoverChanged += OnHover;
            EnsureTooltipReady();
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null) AssociatedObject.HoverChanged -= OnHover;
            Hide();
            base.OnDetaching();
        }

        private static void OnTooltipElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChartTooltipBehavior self) self.EnsureTooltipReady();
        }

        private void EnsureTooltipReady()
        {
            var t = TooltipElement;
            if (t == null) return;
            // 항상 Visible로 두고 Opacity로 보임/숨김 → layout 재계산이 일어나지 않아 깜빡임 사라짐
            t.Visibility = Visibility.Visible;
            t.Opacity = 0;
            t.IsHitTestVisible = false;
            // Margin 대신 RenderTransform으로 위치 변경 (layout 안 거침)
            _translate = t.RenderTransform as TranslateTransform;
            if (_translate == null)
            {
                _translate = new TranslateTransform();
                t.RenderTransform = _translate;
            }
            // 미리 한 번 layout 강제 → RenderSize 채워짐 → 첫 호버에서 정확한 위치
            t.UpdateLayout();
        }

        private void OnHover(object sender, ChartHoverState e)
        {
            if (TooltipElement == null) return;
            if (!e.HasValue) { Hide(); return; }
            if (TitleElement != null) TitleElement.Text = FormatTitle(e);
            if (BodyElement != null) BodyElement.Text = FormatBody(e);
            Position(e.ScreenPoint);
            TooltipElement.Opacity = 1;
        }

        protected virtual string FormatTitle(ChartHoverState e)
            => string.Format(TitleFormat ?? "{0}", e.SeriesTitle ?? "Series");

        protected virtual string FormatBody(ChartHoverState e)
            => string.Format(BodyFormat ?? "X: {0:0.###}  Y: {1:0.###}", e.DataX, e.DataY);

        private void Position(Point hover)
        {
            if (TooltipElement == null || AssociatedObject == null || _translate == null) return;
            var size = TooltipElement.RenderSize;
            if (size.Width <= 0 || size.Height <= 0)
            {
                TooltipElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                size = TooltipElement.DesiredSize;
            }
            var pos = AssociatedObject.ResolveTooltipPosition(hover, size, TooltipMargin);
            _translate.X = pos.X;
            _translate.Y = pos.Y;
        }

        private void Hide()
        {
            if (TooltipElement != null) TooltipElement.Opacity = 0;
        }
    }
}
