using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VSMVVM.WPF.Scheduler.Converters
{
    /// <summary>
    /// 데이터 핀의 ValueType(또는 PinViewModel.ValueType)을 SolidColorBrush로 매핑한다.
    /// 디자인 시스템(VSMVVM.WPF.Design)이 머지되어 있으면 Tailwind 토큰(Red500Brush 등)을 우선 사용하고,
    /// 없으면 폴백 색상으로 처리.
    ///
    /// 매핑 (디자인 토큰 키 / 폴백 RGB):
    ///   bool                       → Red500   / #EF4444
    ///   int, long, short, byte     → Cyan500  / #06B6D4
    ///   float, double, decimal     → Green500 / #22C55E
    ///   string                     → Fuchsia500 / #D946EF
    ///   Guid, DateTime             → Violet500 / #8B5CF6
    ///   void (exec)                → White    / #FFFFFF
    ///   기타                       → Zinc500  / #71717A
    /// </summary>
    public sealed class PinTypeBrushConverter : IValueConverter
    {
        private static readonly Brush BoolBrush     = MakeFrozen("#EF4444");
        private static readonly Brush IntBrush      = MakeFrozen("#06B6D4");
        private static readonly Brush FloatBrush    = MakeFrozen("#22C55E");
        private static readonly Brush StringBrush   = MakeFrozen("#D946EF");
        private static readonly Brush GuidBrush     = MakeFrozen("#8B5CF6");
        private static readonly Brush ExecBrush     = MakeFrozen("#FFFFFF");
        private static readonly Brush DefaultBrush  = MakeFrozen("#71717A");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var type = value as Type;
            if (type == null) return DependencyProperty.UnsetValue;

            // 토큰 우선 검색
            var tokenKey = GetTokenKey(type);
            if (tokenKey != null && Application.Current != null)
            {
                var brush = Application.Current.TryFindResource(tokenKey) as Brush;
                if (brush != null) return brush;
            }

            return GetFallback(type);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static string GetTokenKey(Type t)
        {
            if (t == typeof(void)) return null; // exec — 폴백만
            if (t == typeof(bool)) return "Red500Brush";
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
                || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte))
                return "Cyan500Brush";
            if (t == typeof(float) || t == typeof(double) || t == typeof(decimal)) return "Green500Brush";
            if (t == typeof(string)) return "Fuchsia500Brush";
            if (t == typeof(Guid) || t == typeof(DateTime) || t == typeof(DateTimeOffset)) return "Violet500Brush";
            return "Zinc500Brush";
        }

        private static Brush GetFallback(Type t)
        {
            if (t == typeof(void)) return ExecBrush;
            if (t == typeof(bool)) return BoolBrush;
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
                || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte))
                return IntBrush;
            if (t == typeof(float) || t == typeof(double) || t == typeof(decimal)) return FloatBrush;
            if (t == typeof(string)) return StringBrush;
            if (t == typeof(Guid) || t == typeof(DateTime) || t == typeof(DateTimeOffset)) return GuidBrush;
            return DefaultBrush;
        }

        private static Brush MakeFrozen(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
