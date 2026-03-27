using System;
using System.Globalization;
using System.Resources;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// ResourceManager 기반 로컬라이제이션 서비스 구현체.
    /// </summary>
    internal sealed class LocalizeService : ILocalizeService
    {
        #region Fields

        private ResourceManager _resourceManager;
        private CultureInfo _currentCulture;

        #endregion

        #region Properties

        public string CurrentLocale => _currentCulture?.Name ?? CultureInfo.CurrentUICulture.Name;

        #endregion

        #region Events

        public event Action<string> LocaleChanged;

        #endregion

        #region ILocalizeService

        public void SetResourceManager(ResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        }

        public void ChangeLocale(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
            {
                throw new ArgumentException("Locale must not be null or whitespace.", nameof(locale));
            }

            _currentCulture = new CultureInfo(locale);
            CultureInfo.CurrentUICulture = _currentCulture;
            LocaleChanged?.Invoke(locale);
        }

        public string GetString(string key)
        {
            if (_resourceManager == null)
            {
                return key;
            }

            var culture = _currentCulture ?? CultureInfo.CurrentUICulture;
            return _resourceManager.GetString(key, culture) ?? key;
        }

        #endregion
    }
}
