using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace VSMVVM.WPF.Design.Components.Charts.Behaviors
{
    public class TreemapTooltipBehavior : Behavior<Treemap>
    {
        public static readonly DependencyProperty TooltipElementProperty =
            DependencyProperty.Register(nameof(TooltipElement), typeof(FrameworkElement), typeof(TreemapTooltipBehavior),
                new PropertyMetadata(null, OnTooltipElementChanged));

        public static readonly DependencyProperty TitleElementProperty =
            DependencyProperty.Register(nameof(TitleElement), typeof(TextBlock), typeof(TreemapTooltipBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty BodyElementProperty =
            DependencyProperty.Register(nameof(BodyElement), typeof(TextBlock), typeof(TreemapTooltipBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty TooltipMarginProperty =
            DependencyProperty.Register(nameof(TooltipMargin), typeof(double), typeof(TreemapTooltipBehavior),
                new PropertyMetadata(12.0));

        public FrameworkElement TooltipElement { get => (FrameworkElement)GetValue(TooltipElementProperty); set => SetValue(TooltipElementProperty, value); }
        public TextBlock TitleElement { get => (TextBlock)GetValue(TitleElementProperty); set => SetValue(TitleElementProperty, value); }
        public TextBlock BodyElement { get => (TextBlock)GetValue(BodyElementProperty); set => SetValue(BodyElementProperty, value); }
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
            if (d is TreemapTooltipBehavior self) self.EnsureTooltipReady();
        }

        private void EnsureTooltipReady()
        {
            var t = TooltipElement;
            if (t == null) return;
            t.Visibility = Visibility.Visible;
            t.Opacity = 0;
            t.IsHitTestVisible = false;
            _translate = t.RenderTransform as TranslateTransform;
            if (_translate == null)
            {
                _translate = new TranslateTransform();
                t.RenderTransform = _translate;
            }
            t.UpdateLayout();
        }

        private void OnHover(object sender, TreemapHoverState e)
        {
            if (TooltipElement == null) return;
            if (!e.HasValue) { Hide(); return; }
            if (TitleElement != null) TitleElement.Text = e.Label ?? string.Empty;
            if (BodyElement != null) BodyElement.Text = $"{e.Value:0.##} ({e.Percentage:0.#}%)";
            Position(e.ScreenPoint);
            TooltipElement.Opacity = 1;
        }

        private void Position(Point hover)
        {
            if (TooltipElement == null || AssociatedObject == null || _translate == null) return;
            var size = TooltipElement.RenderSize;
            if (size.Width <= 0 || size.Height <= 0)
            {
                TooltipElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                size = TooltipElement.DesiredSize;
            }
            var w = size.Width;
            var h = size.Height;
            var left = hover.X + TooltipMargin;
            var top = hover.Y + TooltipMargin;
            if (left + w > AssociatedObject.ActualWidth) left = hover.X - w - TooltipMargin;
            if (top + h > AssociatedObject.ActualHeight) top = hover.Y - h - TooltipMargin;
            if (left < 0) left = 0;
            if (top < 0) top = 0;
            _translate.X = left;
            _translate.Y = top;
        }

        private void Hide()
        {
            if (TooltipElement != null) TooltipElement.Opacity = 0;
        }
    }
}
