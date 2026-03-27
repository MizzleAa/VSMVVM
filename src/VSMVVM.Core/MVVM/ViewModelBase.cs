using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// MVVM 패턴의 ViewModel 기본 클래스.
    /// INotifyPropertyChanged, INotifyPropertyChanging 구현.
    /// ConvMVVM2 대비 Undo/Redo 제거 (프로젝트별 구현 권장).
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged, INotifyPropertyChanging
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;

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
