using System;
using System.Globalization;
using System.Windows.Data;

namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// int value &gt; 1 → true, 아니면 false. 다중 선택(카운트 2 이상)을 조건으로 하는 버튼의 IsEnabled 용.
    /// </summary>
    public sealed class GreaterThanOneConverter : IValueConverter
    {
        public static readonly GreaterThanOneConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int n = value switch
            {
                int i => i,
                long l => (int)l,
                _ => 0,
            };
            return n > 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
