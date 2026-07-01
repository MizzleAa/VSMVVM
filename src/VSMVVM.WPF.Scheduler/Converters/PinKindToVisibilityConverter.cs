using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.WPF.Scheduler.Converters
{
    /// <summary>
    /// PinKind 를 Visibility로 변환. PinKindToVisibilityConverter.ExecInstance / DataInstance 로 사용 — 매칭되는 Kind면 Visible, 아니면 Collapsed.
    /// </summary>
    public sealed class PinKindToVisibilityConverter : IValueConverter
    {
        public static readonly PinKindToVisibilityConverter ExecInstance = new() { TargetKind = PinKind.Exec };
        public static readonly PinKindToVisibilityConverter DataInstance = new() { TargetKind = PinKind.Data };

        public PinKind TargetKind { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PinKind k)
                return k == TargetKind ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// bool → Visibility (Visible / Collapsed). System.Windows.Controls.BooleanToVisibilityConverter는 Hidden을 사용하지 않지만,
    /// 본 컨버터도 동일 — 명시 이름으로 노출.
    /// ConverterParameter="Inverse" 시 결과를 뒤집는다 (false→Visible, true→Collapsed).
    /// </summary>
    public sealed class BooleanToVisibilityConverterSafe : IValueConverter
    {
        public static readonly BooleanToVisibilityConverterSafe Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var b = value is bool bv && bv;
            var inverse = parameter is string s &&
                          string.Equals(s, "Inverse", StringComparison.OrdinalIgnoreCase);
            if (inverse) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
