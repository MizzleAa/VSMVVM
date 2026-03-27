using System;
using System.Windows;

namespace VSMVVM.WPF.Behaviors.Base
{
    /// <summary>
    /// 트리거 기본 추상 클래스. 조건 충족 시 Action을 실행합니다.
    /// </summary>
    public abstract class TriggerBase<T> : Freezable where T : DependencyObject
    {
        #region Properties

        /// <summary>
        /// 연결된 대상 객체.
        /// </summary>
        public T AssociatedObject { get; private set; }

        #endregion

        #region Lifecycle

        /// <summary>
        /// 대상에 Trigger를 연결합니다.
        /// </summary>
        public void Attach(T associatedObject)
        {
            AssociatedObject = associatedObject ?? throw new ArgumentNullException(nameof(associatedObject));
            OnAttached();
        }

        /// <summary>
        /// 대상에서 Trigger를 해제합니다.
        /// </summary>
        public void Detach()
        {
            OnDetaching();
            AssociatedObject = null;
        }

        /// <summary>
        /// 연결 시 호출됩니다.
        /// </summary>
        protected abstract void OnAttached();

        /// <summary>
        /// 해제 시 호출됩니다.
        /// </summary>
        protected abstract void OnDetaching();

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
