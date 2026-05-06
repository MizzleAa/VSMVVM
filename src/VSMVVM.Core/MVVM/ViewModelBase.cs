using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// MVVM 패턴의 ViewModel 기본 클래스.
    /// INotifyPropertyChanged, INotifyPropertyChanging, IDisposable 구현.
    /// Subscriptions 컬렉션을 통해 ILocalizeService.Subscribe / StateStoreBase.Subscribe 등의 IDisposable
    /// 토큰을 일괄 관리하고, ViewModel Dispose 시 자동 정리합니다.
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged, INotifyPropertyChanging, IDisposable
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;

        #endregion

        #region Subscriptions

        /// <summary>
        /// 이 ViewModel 의 라이프사이클에 묶인 IDisposable 구독 토큰들.
        /// Dispose() 시 자동으로 모두 해제됩니다.
        /// </summary>
        protected Subscriptions Subscriptions { get; } = new Subscriptions();

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Subscriptions.Dispose();
            }
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// PropertyChanged 이벤트를 발생시킵니다.
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// PropertyChanging 이벤트를 발생시킵니다.
        /// </summary>
        protected virtual void OnPropertyChanging([CallerMemberName] string propertyName = null)
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }

        /// <summary>
        /// 필드 값을 설정하고, 변경 시 PropertyChanged/PropertyChanging 이벤트를 발생시킵니다.
        /// </summary>
        /// <returns>값이 변경되었으면 true, 동일하면 false.</returns>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            OnPropertyChanging(propertyName);
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 추가적인 의존 프로퍼티의 PropertyChanged도 함께 발생시키는 SetProperty 확장.
        /// </summary>
        protected bool SetProperty<T>(ref T storage, T value, string[] additionalPropertyNames, [CallerMemberName] string propertyName = null)
        {
            if (!SetProperty(ref storage, value, propertyName))
                return false;

            if (additionalPropertyNames != null)
            {
                for (int i = 0; i < additionalPropertyNames.Length; i++)
                {
                    OnPropertyChanged(additionalPropertyNames[i]);
                }
            }

            return true;
        }

        #endregion
    }
}
