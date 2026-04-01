using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using VSMVVM.WPF.Controls.Tools;

namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// ICanvasTool → Visibility 변환기.
    /// CanvasToolBase(그리기 도구)이면 Visible, 아니면 Collapsed.
    /// </summary>
    public sealed class ToolToVisibilityConverter : IValueConverter
    {
        public static readonly ToolToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is CanvasToolBase ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// CanvasToolMode → Visibility 변환기.
    /// parameter로 지정한 모드와 일치하면 Visible, 아니면 Collapsed.
    /// </summary>
    public sealed class ToolModeToVisibilityConverter : IValueConverter
    {
        public static readonly ToolModeToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ICanvasTool tool && parameter is string mode)
            {
                return tool.Mode.ToString() == mode ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// ICanvasTool.Mode → bool 변환기 (RadioButton IsChecked용).
    /// parameter로 지정한 모드와 일치하면 true, 아니면 false.
    /// </summary>
    public sealed class ToolModeToBoolConverter : IValueConverter
    {
        public static readonly ToolModeToBoolConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ICanvasTool tool && parameter is string mode)
            {
                return tool.Mode.ToString() == mode;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // OneWay only
            return Binding.DoNothing;
        }
    }
}
