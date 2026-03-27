using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace VSMVVM.WPF.Media
{
    /// <summary>
    /// XAML에서 SVG 파일 경로를 ImageSource로 변환하는 마크업 확장.
    /// 사용법: {media:SvgImage Source=/Assets/icon.svg, Fill={StaticResource TextSecondary}}
    /// </summary>
    [MarkupExtensionReturnType(typeof(ImageSource))]
    public sealed class SvgImageExtension : MarkupExtension
    {
        #region Properties

        /// <summary>
        /// SVG 리소스 경로.
        /// </summary>
        [ConstructorArgument("source")]
        public string Source { get; set; }

        /// <summary>
        /// SVG 채우기 색상 오버라이드. 지정 시 모든 도형의 fill을 이 색상으로 대체합니다.
        /// </summary>
        public Brush Fill { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// 기본 생성자.
        /// </summary>
        public SvgImageExtension()
        {
        }

        /// <summary>
        /// 리소스 경로를 지정하는 생성자.
        /// </summary>
        public SvgImageExtension(string source)
        {
            Source = source;
        }

        #endregion

        #region MarkupExtension

        /// <summary>
        /// SVG를 ImageSource로 변환하여 반환합니다.
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Source))
            {
                return null;
            }

            try
            {
                DrawingImage result;

                // pack:// URI 또는 일반 파일 경로 지원
                if (Source.StartsWith("pack://") || Source.StartsWith("/"))
                {
                    Uri uri;
                    if (Source.StartsWith("/"))
                    {
                        uri = new Uri($"pack://application:,,,{Source}", UriKind.Absolute);
                    }
                    else
                    {
                        uri = new Uri(Source, UriKind.Absolute);
                    }

                    result = SvgImageConverter.ConvertFromResource(uri);
                }
                else
                {
                    result = SvgImageConverter.ConvertFromFile(Source);
                }

                // Fill 오버라이드 적용
                if (result != null && Fill != null)
                {
                    SvgImageConverter.OverrideFill(result.Drawing as DrawingGroup, Fill);
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
