using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// Redux 스타일 전역 상태 스토어 추상 클래스.
    /// 상태 변경 시 구독자에게 자동 통지합니다. WeakReference 기반 메모리 누수 방지.
    /// </summary>
    public abstract class StateStoreBase<TState> : INotifyPropertyChanged where TState : class
    {
        #region Fields

        private TState _state;
        private readonly List<WeakReference<Action<TState>>> _subscribers = new List<WeakReference<Action<TState>>>();
        private readonly object _lock = new object();

        #endregion

        #region Constructors

        /// <summary>
        /// 초기 상태로 스토어를 생성합니다.
        /// </summary>
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

        /// <summary>
        /// PropertyChanged 이벤트를 발생시킵니다.
        /// </summary>
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

            _state = newState;
            OnPropertyChanged(nameof(State));
            NotifySubscribers();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 상태 변경을 구독합니다. WeakReference 기반으로 메모리 누수를 방지합니다.
        /// </summary>
        public void Subscribe(Action<TState> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            lock (_lock)
            {
                _subscribers.Add(new WeakReference<Action<TState>>(callback));
            }

            // 현재 상태를 즉시 통지
            callback(_state);
        }

        /// <summary>
        /// 구독을 해제합니다.
        /// </summary>
        public void Unsubscribe(Action<TState> callback)
        {
            if (callback == null)
            {
                return;
            }

            lock (_lock)
            {
                _subscribers.RemoveAll(wr =>
                {
                    if (!wr.TryGetTarget(out var target))
                    {
                        return true; // 이미 GC 수집됨
                    }

                    return ReferenceEquals(target, callback);
                });
            }
        }

        #endregion

        #region Private Methods

        private void NotifySubscribers()
        {
            List<WeakReference<Action<TState>>> snapshot;

            lock (_lock)
            {
                snapshot = new List<WeakReference<Action<TState>>>(_subscribers);
            }

            var deadRefs = new List<WeakReference<Action<TState>>>();

            foreach (var weakRef in snapshot)
            {
                if (weakRef.TryGetTarget(out var callback))
                {
                    callback(_state);
                }
                else
                {
                    deadRefs.Add(weakRef);
                }
            }

            // 죽은 참조 정리
            if (deadRefs.Count > 0)
            {
                lock (_lock)
                {
                    foreach (var dead in deadRefs)
                    {
                        _subscribers.Remove(dead);
                    }
                }
            }
        }

        #endregion
    }
}
