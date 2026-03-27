using System;
using System.Windows;

namespace VSMVVM.WPF.Behaviors.Base
{
    /// <summary>
    /// 트리거에 의해 실행되는 액션 기본 추상 클래스.
    /// </summary>
    public abstract class TriggerAction<T> : Freezable where T : DependencyObject
    {
        #region Properties

        /// <summary>
        /// 연결된 대상 객체.
        /// </summary>
        public T AssociatedObject { get; internal set; }

        #endregion

        #region Abstract

        /// <summary>
        /// 트리거 발동 시 실행할 로직.
        /// </summary>
        protected abstract void Invoke(object parameter);

        #endregion

        #region Public Methods

        /// <summary>
        /// 외부에서 액션을 실행합니다.
        /// </summary>
        public void Execute(object parameter)
        {
            Invoke(parameter);
        }

        #endregion

        #region Freezable

        /// <summary>
        /// Freezable 인스턴스를 생성합니다.
        /// </summary>
        protected override Freezable CreateInstanceCore()
        {
            return (Freezable)Activator.CreateInstance(GetType());
        }

        #endregion
    }
}
