using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Sample.Behaviors
{
    /// <summary>
    /// VM의 폰트 패밀리/크기 → 여러 ChartAxis 객체에 일괄 동기화한다.
    /// </summary>
    public sealed class AxisFontSyncBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty SourceFamilyProperty =
            DependencyProperty.Register(nameof(SourceFamily), typeof(FontFamily), typeof(AxisFontSyncBehavior),
                new PropertyMetadata(null, OnAnyChanged));

        public static readonly DependencyProperty SourceSizeProperty =
            DependencyProperty.Register(nameof(SourceSize), typeof(double), typeof(AxisFontSyncBehavior),
                new PropertyMetadata(11.0, OnAnyChanged));

        public static readonly DependencyProperty AxesProperty =
            DependencyProperty.Register(nameof(Axes), typeof(IList<ChartAxis>), typeof(AxisFontSyncBehavior),
                new PropertyMetadata(null, OnAnyChanged));

        public FontFamily SourceFamily { get => (FontFamily)GetValue(SourceFamilyProperty); set => SetValue(SourceFamilyProperty, value); }
        public double SourceSize { get => (double)GetValue(SourceSizeProperty); set => SetValue(SourceSizeProperty, value); }
        public IList<ChartAxis> Axes { get => (IList<ChartAxis>)GetValue(AxesProperty); set => SetValue(AxesProperty, value); }

        private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AxisFontSyncBehavior self) self.Apply();
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            Apply();
        }

        private void Apply()
        {
            var axes = Axes;
            if (axes == null) return;
            var f = SourceFamily;
            var s = SourceSize;
            foreach (var ax in axes)
            {
                if (ax == null) continue;
                if (f != null) ax.FontFamily = f;
                ax.FontSize = s;
                ax.TitleFontSize = s + 2;
            }
        }
    }
}
