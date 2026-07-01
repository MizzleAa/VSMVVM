using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.WPF.Scheduler.Converters
{
    /// <summary>
    /// I.4 — SchedulerLogLevel → 디자인 토큰 브러시.
    /// Trace/Debug = TextMuted, Info = TextSecondary, Warning = Warning, Error = Error.
    /// 토큰 키는 FrameworkElement Resource 에서 조회. 없으면 Foreground 흰색 fallback.
    /// </summary>
    public sealed class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is SchedulerLogLevel lvl)) return Brushes.White;
            string key = lvl switch
            {
                SchedulerLogLevel.Trace => "TextMuted",
                SchedulerLogLevel.Debug => "TextMuted",
                SchedulerLogLevel.Info => "TextSecondary",
                SchedulerLogLevel.Warning => "Warning",
                SchedulerLogLevel.Error => "Error",
                _ => "TextSecondary",
            };
            var res = Application.Current?.TryFindResource(key);
            if (res is Brush b) return b;
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
