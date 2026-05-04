using System;
using System.Windows;

namespace VSMVVM.WPF.Behaviors.Base
{
    /// <summary>
    /// WPF Behavior 기본 추상 클래스.
    /// DependencyObject를 대상으로 연결/해제 라이프사이클을 제공합니다.
    /// </summary>
    public abstract class Behavior<T> : Freezable where T : DependencyObject
    {
        #region Properties

        /// <summary>
        /// 연결된 대상 객체.
        /// </summary>
        public T AssociatedObject { get; private set; }

        #endregion

        #region Lifecycle

        /// <summary>
        /// 대상에 Behavior를 연결합니다.
        /// </summary>
        public void Attach(T associatedObject)
        {
            AssociatedObject = associatedObject ?? throw new ArgumentNullException(nameof(associatedObject));
            OnAttached();
        }

        /// <summary>
        /// 대상에서 Behavior를 해제합니다.
        /// </summary>
        public void Detach()
        {
            OnDetaching();
            AssociatedObject = null;
        }

        /// <summary>
        /// 연결 시 호출됩니다. 자식 클래스가 base.OnAttached()를 호출할 수 있도록 virtual.
        /// </summary>
        protected virtual void OnAttached() { }

        /// <summary>
        /// 해제 시 호출됩니다. 자식 클래스가 base.OnDetaching()을 호출할 수 있도록 virtual.
        /// </summary>
        protected virtual void OnDetaching() { }

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
