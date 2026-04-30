using System.Windows;
using Microsoft.Xaml.Behaviors;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts.Behaviors
{
    public sealed class FitTriggerBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty FitTriggerProperty =
            DependencyProperty.Register(nameof(FitTrigger), typeof(object), typeof(FitTriggerBehavior),
                new PropertyMetadata(null, OnFitTriggerChanged));

        public object FitTrigger { get => GetValue(FitTriggerProperty); set => SetValue(FitTriggerProperty, value); }

        private static void OnFitTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FitTriggerBehavior self && self.AssociatedObject is IFittable fittable)
            {
                fittable.FitToContent();
            }
        }
    }
}
