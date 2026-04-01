using System;
using System.Globalization;
using System.Windows.Data;

namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// bool → double 변환기. true=1.0, false=0.3.
    /// 레이어 가시성 토글 등에서 사용합니다.
    /// </summary>
    public sealed class BoolToOpacityConverter : IValueConverter
    {
        public static readonly BoolToOpacityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? 1.0 : 0.3;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
