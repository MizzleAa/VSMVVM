using System;
using System.Windows;

namespace VSMVVM.WPF.Behaviors.Base
{
    /// <summary>
    /// 비제네릭 Behavior 베이스. XAML 컬렉션에서 모든 Behavior를 단일 타입으로
    /// 다루기 위한 추상 진입점이며, 일반 Behavior&lt;T&gt;가 이 클래스를 상속한다.
    /// 외부 호출자(컬렉션 부착 속성)는 <see cref="AttachInternal(DependencyObject)"/>로
    /// 대상에 연결한다.
    /// </summary>
    public abstract class BehaviorBase : Freezable
    {
        /// <summary>대상에 Behavior를 연결합니다. 컬렉션 호스트가 호출.</summary>
        public abstract void AttachInternal(DependencyObject associatedObject);

        /// <summary>대상에서 Behavior를 해제합니다.</summary>
        public abstract void DetachInternal();
    }

    /// <summary>
    /// WPF Behavior 기본 추상 클래스.
    /// DependencyObject를 대상으로 연결/해제 라이프사이클을 제공합니다.
    /// </summary>
    public abstract class Behavior<T> : BehaviorBase where T : DependencyObject
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

        /// <inheritdoc/>
        public override void AttachInternal(DependencyObject associatedObject)
        {
            if (associatedObject is T typed)
            {
                Attach(typed);
            }
            else
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} requires AssociatedObject of type {typeof(T).Name}, got {associatedObject?.GetType().Name ?? "null"}.");
            }
        }

        /// <inheritdoc/>
        public override void DetachInternal() => Detach();

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
