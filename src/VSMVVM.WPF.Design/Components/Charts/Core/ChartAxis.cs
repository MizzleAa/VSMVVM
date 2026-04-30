using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public class ChartAxis : DependencyObject
    {
        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(ChartAxis),
                new PropertyMetadata(new FontFamily("Segoe UI"), OnAxisChanged));

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(ChartAxis),
                new PropertyMetadata(11.0, OnAxisChanged));

        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(ChartAxis),
                new PropertyMetadata(FontWeights.Normal, OnAxisChanged));

        public static readonly DependencyProperty TitleFontSizeProperty =
            DependencyProperty.Register(nameof(TitleFontSize), typeof(double), typeof(ChartAxis),
                new PropertyMetadata(13.0, OnAxisChanged));

        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
        public double TitleFontSize { get => (double)GetValue(TitleFontSizeProperty); set => SetValue(TitleFontSizeProperty, value); }

        public static readonly DependencyProperty MinProperty =
            DependencyProperty.Register(nameof(Min), typeof(double), typeof(ChartAxis),
                new PropertyMetadata(double.NaN, OnAxisChanged));

        public static readonly DependencyProperty MaxProperty =
            DependencyProperty.Register(nameof(Max), typeof(double), typeof(ChartAxis),
                new PropertyMetadata(double.NaN, OnAxisChanged));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ChartAxis),
                new PropertyMetadata(string.Empty, OnAxisChanged));

        public static readonly DependencyProperty LabelFormatProperty =
            DependencyProperty.Register(nameof(LabelFormat), typeof(string), typeof(ChartAxis),
                new PropertyMetadata(null, OnAxisChanged));

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(nameof(IsVisible), typeof(bool), typeof(ChartAxis),
                new PropertyMetadata(true, OnAxisChanged));

        public static readonly DependencyProperty IsCategoricalProperty =
            DependencyProperty.Register(nameof(IsCategorical), typeof(bool), typeof(ChartAxis),
                new PropertyMetadata(false, OnAxisChanged));

        public static readonly DependencyProperty CategoriesProperty =
            DependencyProperty.Register(nameof(Categories), typeof(IList<string>), typeof(ChartAxis),
                new PropertyMetadata(null, OnAxisChanged));

        public static readonly DependencyProperty LabelAngleProperty =
            DependencyProperty.Register(nameof(LabelAngle), typeof(double), typeof(ChartAxis),
                new PropertyMetadata(double.NaN, OnAxisChanged));

        public static readonly DependencyProperty MajorTickCountTargetProperty =
            DependencyProperty.Register(nameof(MajorTickCountTarget), typeof(int), typeof(ChartAxis),
                new PropertyMetadata(8, OnAxisChanged));

        public double Min { get => (double)GetValue(MinProperty); set => SetValue(MinProperty, value); }
        public double Max { get => (double)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public string LabelFormat { get => (string)GetValue(LabelFormatProperty); set => SetValue(LabelFormatProperty, value); }
        public bool IsVisible { get => (bool)GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
        public bool IsCategorical { get => (bool)GetValue(IsCategoricalProperty); set => SetValue(IsCategoricalProperty, value); }
        public IList<string> Categories { get => (IList<string>)GetValue(CategoriesProperty); set => SetValue(CategoriesProperty, value); }
        public double LabelAngle { get => (double)GetValue(LabelAngleProperty); set => SetValue(LabelAngleProperty, value); }
        public int MajorTickCountTarget { get => (int)GetValue(MajorTickCountTargetProperty); set => SetValue(MajorTickCountTargetProperty, value); }

        public event EventHandler Changed;

        public bool IsAutoMin => double.IsNaN(Min);
        public bool IsAutoMax => double.IsNaN(Max);

        public string Format(double value)
        {
            var fmt = LabelFormat;
            if (string.IsNullOrEmpty(fmt))
            {
                var abs = Math.Abs(value);
                if (abs != 0 && (abs >= 1e6 || abs < 1e-3)) return value.ToString("0.##E+0");
                if (abs >= 100) return value.ToString("0");
                if (abs >= 1) return value.ToString("0.##");
                return value.ToString("0.###");
            }
            return value.ToString(fmt);
        }

        private static void OnAxisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChartAxis a) a.Changed?.Invoke(a, EventArgs.Empty);
        }
    }
}
