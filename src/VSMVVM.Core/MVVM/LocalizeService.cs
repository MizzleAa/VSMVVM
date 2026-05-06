using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// ResourceManager 기반 로컬라이제이션 서비스 구현체.
    /// Subscribe → IDisposable 패턴 (strong-ref). 호출자가 IDisposable 보관 책임.
    /// </summary>
    internal sealed class LocalizeService : ILocalizeService
    {
        #region Fields

        private ResourceManager _resourceManager;
        private CultureInfo _currentCulture;

        private readonly List<Action<string>> _subscribers = new();
        private readonly object _lock = new();

        #endregion

        #region Properties

        public string CurrentLocale => _currentCulture?.Name ?? CultureInfo.CurrentUICulture.Name;

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

        public IDisposable Subscribe(Action<string> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            lock (_lock)
            {
                _subscribers.Add(callback);
            }

            return new Subscription(this, callback);
        }

        #endregion

        #region Private Methods

        private void Unsubscribe(Action<string> callback)
        {
            lock (_lock)
            {
                _subscribers.Remove(callback);
            }
        }

        private void RaiseLocaleChanged(string locale)
        {
            List<Action<string>> snapshot;
            lock (_lock)
            {
                snapshot = new List<Action<string>>(_subscribers);
            }

            foreach (var callback in snapshot)
            {
                // 한 핸들러의 예외가 다른 모든 핸들러를 막지 않도록 격리.
                try
                {
                    callback(locale);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalizeService] Locale callback threw: {ex}");
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private LocalizeService _service;
            private Action<string> _callback;

            public Subscription(LocalizeService service, Action<string> callback)
            {
                _service = service;
                _callback = callback;
            }

            public void Dispose()
            {
                var service = System.Threading.Interlocked.Exchange(ref _service, null);
                var callback = System.Threading.Interlocked.Exchange(ref _callback, null);
                if (service != null && callback != null)
                {
                    service.Unsubscribe(callback);
                }
            }
        }

        #endregion
    }
}