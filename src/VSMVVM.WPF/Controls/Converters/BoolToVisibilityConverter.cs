using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// bool → Visibility. true=Visible, false=Collapsed.
    /// parameter="Inverse" 또는 ConverterParameter가 "Inverse"/"Invert"/"!"이면 반전.
    /// </summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public static readonly BoolToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool b && b;
            if (IsInverse(parameter)) flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is Visibility v && v == Visibility.Visible;
            if (IsInverse(parameter)) flag = !flag;
            return flag;
        }

        private static bool IsInverse(object parameter)
        {
            var s = parameter as string;
            return string.Equals(s, "Inverse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Invert", StringComparison.OrdinalIgnoreCase)
                || s == "!";
        }
    }

    /// <summary>
    /// bool → Visibility. true=Collapsed, false=Visible. (반전 변형, 항상 사용 가능)
    /// </summary>
    public sealed class BoolToVisibilityInverseConverter : IValueConverter
    {
        public static readonly BoolToVisibilityInverseConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => !(value is Visibility v && v == Visibility.Visible);
    }
}
