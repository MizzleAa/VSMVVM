using System;
using System.Collections.Generic;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// IDisposable 구독 토큰들을 일괄 관리합니다.
    /// ViewModelBase.Dispose()에서 자동으로 정리되어 unsubscribe boilerplate를 제거합니다.
    /// Subscriptions 가 이미 Dispose 된 후 Add 호출 시 즉시 dispose 합니다 (race-safe).
    /// </summary>
    public sealed class Subscriptions : IDisposable
    {
        private readonly List<IDisposable> _items = new();
        private readonly object _lock = new();
        private bool _disposed;

        /// <summary>
        /// 구독 토큰을 추가합니다. Subscriptions 가 이미 Dispose 된 경우 즉시 dispose 됩니다.
        /// </summary>
        public IDisposable Add(IDisposable disposable)
        {
            if (disposable == null) return EmptyDisposable.Instance;

            lock (_lock)
            {
                if (_disposed)
                {
                    disposable.Dispose();
                    return disposable;
                }
                _items.Add(disposable);
            }
            return disposable;
        }

        public void Dispose()
        {
            List<IDisposable> snapshot;
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                snapshot = new List<IDisposable>(_items);
                _items.Clear();
            }

            foreach (var item in snapshot)
            {
                try { item.Dispose(); } catch { /* 한 핸들러의 예외가 다른 정리를 막지 않도록 */ }
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            public void Dispose() { }
        }
    }
}