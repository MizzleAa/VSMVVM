using System;
using System.Collections.Generic;
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

        // LocalizeService는 싱글톤이라 strong-ref 이벤트는 구독한 View들을 영구히 잡아 메모리 누수를 일으킨다.
        // 동일 이벤트 시그니처를 유지하면서 내부적으로 약참조 subscription 리스트로 보관한다.
        private readonly List<WeakReference<Action<string>>> _localeChangedSubscribers = new List<WeakReference<Action<string>>>();
        private readonly object _subscribersLock = new object();

        #endregion

        #region Properties

        public string CurrentLocale => _currentCulture?.Name ?? CultureInfo.CurrentUICulture.Name;

        #endregion

        #region Events

        public event Action<string> LocaleChanged
        {
            add
            {
                if (value == null) return;
                lock (_subscribersLock)
                {
                    _localeChangedSubscribers.Add(new WeakReference<Action<string>>(value));
                }
            }
            remove
            {
                if (value == null) return;
                lock (_subscribersLock)
                {
                    _localeChangedSubscribers.RemoveAll(wr =>
                    {
                        if (!wr.TryGetTarget(out var target)) return true;
                        return ReferenceEquals(target, value);
                    });
                }
            }
        }

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
            RaiseLocaleChanged(locale);
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

        #region Private Methods

        private void RaiseLocaleChanged(string locale)
        {
            List<WeakReference<Action<string>>> snapshot;
            lock (_subscribersLock)
            {
                snapshot = new List<WeakReference<Action<string>>>(_localeChangedSubscribers);
            }

            List<WeakReference<Action<string>>> dead = null;
            foreach (var weakRef in snapshot)
            {
                if (weakRef.TryGetTarget(out var callback))
                {
                    callback(locale);
                }
                else
                {
                    (dead ??= new List<WeakReference<Action<string>>>()).Add(weakRef);
                }
            }

            if (dead != null)
            {
                lock (_subscribersLock)
                {
                    foreach (var d in dead)
                    {
                        _localeChangedSubscribers.Remove(d);
                    }
                }
            }
        }

        #endregion
    }
}
