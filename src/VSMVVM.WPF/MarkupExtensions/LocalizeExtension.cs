using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.MarkupExtensions
{
    /// <summary>
    /// XAML에서 리소스 키로 로컬라이즈된 문자열을 바인딩하는 마크업 확장.
    /// ChangeLocale() 호출 시 자동으로 UI가 갱신됩니다.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocalizeExtension : MarkupExtension
    {
        #region Properties

        /// <summary>
        /// 리소스 키.
        /// </summary>
        [ConstructorArgument("key")]
        public string Key { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// 기본 생성자.
        /// </summary>
        public LocalizeExtension()
        {
        }

        /// <summary>
        /// 리소스 키를 지정하는 생성자.
        /// </summary>
        public LocalizeExtension(string key)
        {
            Key = key;
        }

        #endregion

        #region MarkupExtension

        /// <summary>
        /// 바인딩 값을 제공합니다.
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
            {
                return string.Empty;
            }

            try
            {
                var localizeService = ServiceLocator.GetServiceProvider().GetService<ILocalizeService>();
                return localizeService.GetString(Key);
            }
            catch
            {
                return Key;
            }
        }

        #endregion
    }
}
