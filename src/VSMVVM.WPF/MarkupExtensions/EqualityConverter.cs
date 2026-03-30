using System;
using System.Globalization;
using System.Windows.Data;

namespace VSMVVM.WPF.MarkupExtensions
{
    /// <summary>
    /// 두 값의 동일 여부를 bool로 반환하는 MultiValueConverter.
    /// DataTrigger에서 두 바인딩 값 비교 시 사용합니다.
    /// </summary>
    /// <example>
    /// <![CDATA[
    /// <DataTrigger Value="True">
    ///     <DataTrigger.Binding>
    ///         <MultiBinding Converter="{x:Static me:EqualityConverter.Instance}">
    ///             <Binding Path="ActiveMenu"/>
    ///             <Binding Path="Tag" RelativeSource="{RelativeSource Self}"/>
    ///         </MultiBinding>
    ///     </DataTrigger.Binding>
    ///     <Setter Property="Background" Value="..."/>
    /// </DataTrigger>
    /// ]]>
    /// </example>
    public sealed class EqualityConverter : IMultiValueConverter
    {
        /// <summary>
        /// 싱글톤 인스턴스. XAML에서 x:Static으로 참조합니다.
        /// </summary>
        public static readonly EqualityConverter Instance = new EqualityConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
            {
                return false;
            }

            return Equals(values[0], values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
