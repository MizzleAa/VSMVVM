using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// value.ToString() == parameter 이면 Visible, 아니면 Collapsed.
    /// Measurement.Unit("px"/"°") 같은 tag 문자열로 ItemTemplate 내부에서 아이콘 등을 토글하는 용도.
    /// </summary>
    public sealed class StringEqualsToVisibilityConverter : IValueConverter
    {
        public static readonly StringEqualsToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var left = value?.ToString();
            var right = parameter?.ToString();
            return string.Equals(left, right, StringComparison.Ordinal)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
