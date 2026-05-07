using System;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.MarkupExtensions
{
    /// <summary>
    /// XAML에서 리소스 키로 로컬라이즈된 문자열을 바인딩하는 마크업 확장.
    /// ChangeLocale() 호출 시 LocalizeService.Subscribe 콜백을 받아 자동으로 UI가 갱신됩니다.
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

        /// <summary>
        /// 선택적 string.Format 패턴. 지정 시 GetString(Key) 결과를 string.Format(StringFormat, value)로 감쌉니다.
        /// 예: StringFormat="[{0}]"
        /// </summary>
        public string StringFormat { get; set; }

        #endregion

        #region Constructors

        public LocalizeExtension() { }

        public LocalizeExtension(string key)
        {
            Key = key;
        }

        #endregion

        #region MarkupExtension

        /// <summary>
        /// 동적 바인딩 값을 제공합니다. ProvideValue 시점에 LocalizeProxy 인스턴스를 만들어
        /// Source 로 둔 OneWay Binding 을 반환 → ChangeLocale 시 Subscribe 콜백 → PropertyChanged → UI 자동 갱신.
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
            {
                return string.Empty;
            }

            ILocalizeService localize;
            try
            {
                localize = ServiceLocator.GetServiceProvider().GetService<ILocalizeService>();
            }
            catch
            {
                return Key;
            }

            if (localize == null)
            {
                return Key;
            }

            var proxy = new LocalizeProxy(Key, StringFormat, localize);

            var binding = new Binding(nameof(LocalizeProxy.Value))
            {
                Source = proxy,
                Mode = BindingMode.OneWay
            };
            return binding.ProvideValue(serviceProvider);
        }

        #endregion
    }

    /// <summary>
    /// LocalizeExtension 의 동적 갱신을 매개하는 INPC 프록시.
    /// LocalizeService.Subscribe 로 LocaleChanged 를 듣고 Value 의 PropertyChanged 를 발화한다.
    /// Binding.Source 로 바인딩되므로 strong-reference 가 보장되어 lifetime 안전.
    /// </summary>
    internal sealed class LocalizeProxy : INotifyPropertyChanged
    {
        private readonly string _key;
        private readonly string _stringFormat;
        private readonly ILocalizeService _localize;
        private readonly IDisposable _subscription;

        public LocalizeProxy(string key, string stringFormat, ILocalizeService localize)
        {
            _key = key;
            _stringFormat = stringFormat;
            _localize = localize;
            _subscription = _localize.Subscribe(_ => RaiseValueChanged());
        }

        public string Value
        {
            get
            {
                var raw = _localize.GetString(_key);
                if (!string.IsNullOrEmpty(_stringFormat))
                {
                    return string.Format(_stringFormat, raw);
                }
                return raw;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaiseValueChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }
}