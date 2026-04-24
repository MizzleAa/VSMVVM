using System;
using System.Globalization;
using System.Windows.Data;

namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// Popup.HorizontalOffset 계산 — PlacementTarget 의 우측을 Popup 의 우측에 정렬한다.
    /// value = PlacementTarget.ActualWidth, parameter = Popup 컨텐츠 너비(double).
    /// 결과 = value - popupWidth (음수). 결과 ≤ 0 일 때 Popup 이 좌측으로 밀려 우측 정렬됨.
    /// </summary>
    public sealed class RightAlignOffsetConverter : IValueConverter
    {
        public static readonly RightAlignOffsetConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double targetWidth = value is double w ? w : 0;
            double popupWidth = 0;
            if (parameter is double p) popupWidth = p;
            else if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var sp)) popupWidth = sp;
            return targetWidth - popupWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
