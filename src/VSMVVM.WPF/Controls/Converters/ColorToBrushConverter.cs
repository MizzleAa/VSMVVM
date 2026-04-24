using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// <see cref="Color"/> → <see cref="SolidColorBrush"/> 변환기.
    /// ItemTemplate 내부에서 inline SolidColorBrush 의 Color 바인딩이 DataContext 를
    /// 못 찾는 WPF 한계를 피하기 위해 Brush 단위로 바인딩한다.
    /// </summary>
    public sealed class ColorToBrushConverter : IValueConverter
    {
        public static readonly ColorToBrushConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color c)
            {
                var brush = new SolidColorBrush(c);
                brush.Freeze();
                return brush;
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
