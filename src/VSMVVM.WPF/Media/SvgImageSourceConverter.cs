using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

#nullable enable

namespace VSMVVM.WPF.Media
{
    /// <summary>
    /// SVG 리소스 Uri 또는 pack URI 문자열을 WPF ImageSource로 변환하는 IValueConverter.
    /// ItemTemplate 등 데이터 바인딩 시나리오에서 사용합니다.
    /// 선택적으로 ConverterParameter로 SolidColorBrush hex 색상을 넘겨 fill 오버라이드 가능.
    /// </summary>
    public sealed class SvgImageSourceConverter : IValueConverter
    {
        /// <summary>
        /// SVG 채우기 색상 오버라이드. 지정 시 모든 도형의 fill을 이 색상으로 대체합니다.
        /// XAML에서 StaticResource로 지정 가능.
        /// </summary>
        public Brush? Fill { get; set; }

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Uri? uri = value switch
            {
                Uri u => u,
                string s when !string.IsNullOrEmpty(s) => CreateUri(s),
                _ => null
            };

            if (uri == null)
            {
                return null;
            }

            DrawingImage? image;
            try
            {
                image = SvgImageConverter.ConvertFromResource(uri);
            }
            catch
            {
                return null;
            }

            if (image == null)
            {
                return null;
            }

            // ConverterParameter가 색상 문자열로 넘어오면 fill 오버라이드 (#RRGGBB 등)
            Brush? overrideFill = Fill;
            if (parameter is string paramHex && !string.IsNullOrEmpty(paramHex))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(paramHex);
                    overrideFill = new SolidColorBrush(color);
                }
                catch
                {
                    // ignore parse failure, fall back to Fill property
                }
            }

            if (overrideFill != null && image.Drawing is DrawingGroup dg)
            {
                SvgImageConverter.OverrideFill(dg, overrideFill);
            }

            return image;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static Uri? CreateUri(string source)
        {
            try
            {
                if (source.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                {
                    return new Uri(source, UriKind.Absolute);
                }
                if (source.StartsWith("/"))
                {
                    return new Uri($"pack://application:,,,{source}", UriKind.Absolute);
                }
                return new Uri(source, UriKind.RelativeOrAbsolute);
            }
            catch
            {
                return null;
            }
        }
    }
}
