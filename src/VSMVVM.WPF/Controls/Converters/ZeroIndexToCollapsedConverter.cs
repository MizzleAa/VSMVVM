using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// int == 0 → <see cref="Visibility.Collapsed"/>, 그 외 → <see cref="Visibility.Visible"/>.
    /// Background 라벨(Index=0) 행에서 편집 아이콘 등 편집용 UI 를 숨기는 용도.
    /// </summary>
    public sealed class ZeroIndexToCollapsedConverter : IValueConverter
    {
        public static readonly ZeroIndexToCollapsedConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int i = value switch
            {
                int v => v,
                _ => 0,
            };
            return i == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// int != 0 → true (편집 가능), Index == 0 → false. HitTest/IsEnabled 바인딩용.
    /// </summary>
    public sealed class NotZeroIndexConverter : IValueConverter
    {
        public static readonly NotZeroIndexConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int i = value switch
            {
                int v => v,
                _ => 0,
            };
            return i != 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
