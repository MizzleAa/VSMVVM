using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// Redux 스타일 전역 상태 스토어 추상 클래스.
    /// Subscribe(Action) → IDisposable 패턴. strong-ref 보관 + Dispose 토큰으로 명시적 정리.
    /// </summary>
    public abstract class StateStoreBase<TState> : INotifyPropertyChanged where TState : class
    {
        #region Fields

        private TState _state;
        private readonly List<Action<TState>> _subscribers = new();
        private readonly object _lock = new();

        #endregion

        #region Constructors

        protected StateStoreBase(TState initialState)
        {
            _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
        }

        #endregion

        #region Properties

        /// <summary>
        /// 현재 상태.
        /// </summary>
        public TState State => _state;

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// 상태를 업데이트하고 모든 구독자에게 통지합니다.
        /// </summary>
        protected void UpdateState(TState newState)
        {
            if (newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }

            // _state 할당과 알림 사이가 노출되면 두 스레드의 UpdateState가 인터리빙되어
            // subscriber가 받는 상태와 최종 _state가 어긋날 수 있다. lock으로 직렬화한다.
            TState snapshotState;
            lock (_lock)
            {
                _state = newState;
                snapshotState = newState;
            }

            OnPropertyChanged(nameof(State));
            NotifySubscribers(snapshotState);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 상태 변경을 구독합니다. 즉시 1회 현재 상태로 콜백 실행 + 이후 변경 시마다 통지.
        /// 반환된 IDisposable.Dispose() 호출 시 구독 해제. 콜백은 strong-ref 로 보관됨.
        /// </summary>
        public IDisposable Subscribe(Action<TState> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            // Add + 즉시 통지를 같은 lock 안에서 처리해야 UpdateState와 순서가 어긋나지 않는다.
            lock (_lock)
            {
                _subscribers.Add(callback);
                callback(_state);
            }

            return new Subscription(this, callback);
        }

        #endregion

        #region Private Methods

        private void Unsubscribe(Action<TState> callback)
        {
            lock (_lock)
            {
                _subscribers.Remove(callback);
            }
        }

        private void NotifySubscribers(TState stateToNotify)
        {
            List<Action<TState>> snapshot;
            lock (_lock)
            {
                snapshot = new List<Action<TState>>(_subscribers);
            }

            foreach (var callback in snapshot)
            {
                // _state가 아니라 호출자가 본 그 시점의 state를 통지해야 알림과 값이 일관된다.
                try
                {
                    callback(stateToNotify);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StateStoreBase] Subscriber callback threw: {ex}");
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private StateStoreBase<TState> _store;
            private Action<TState> _callback;

            public Subscription(StateStoreBase<TState> store, Action<TState> callback)
            {
                _store = store;
                _callback = callback;
            }

            public void Dispose()
            {
                var store = System.Threading.Interlocked.Exchange(ref _store, null);
                var callback = System.Threading.Interlocked.Exchange(ref _callback, null);
                if (store != null && callback != null)
                {
                    store.Unsubscribe(callback);
                }
            }
        }

        #endregion
    }
}