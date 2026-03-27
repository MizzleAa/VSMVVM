using System;
using System.Reflection;
using System.Windows;
using VSMVVM.WPF.Behaviors.Base;

namespace VSMVVM.WPF.Behaviors.Triggers
{
    /// <summary>
    /// 이벤트 발생 시 연결된 TriggerAction을 실행하는 트리거.
    /// </summary>
    public sealed class EventTrigger : TriggerBase<FrameworkElement>
    {
        #region DependencyProperties

        /// <summary>
        /// 감시할 이벤트 이름 종속성 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty EventNameProperty =
            DependencyProperty.Register(
                nameof(EventName),
                typeof(string),
                typeof(EventTrigger),
                new PropertyMetadata(null));

        /// <summary>
        /// 실행할 액션 종속성 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty ActionProperty =
            DependencyProperty.Register(
                nameof(Action),
                typeof(TriggerAction<DependencyObject>),
                typeof(EventTrigger),
                new PropertyMetadata(null));

        /// <summary>
        /// 감시할 이벤트 이름.
        /// </summary>
        public string EventName
        {
            get => (string)GetValue(EventNameProperty);
            set => SetValue(EventNameProperty, value);
        }

        /// <summary>
        /// 트리거 발동 시 실행할 액션.
        /// </summary>
        public TriggerAction<DependencyObject> Action
        {
            get => (TriggerAction<DependencyObject>)GetValue(ActionProperty);
            set => SetValue(ActionProperty, value);
        }

        #endregion

        #region Fields

        private Delegate _eventHandler;
        private EventInfo _eventInfo;

        #endregion

        #region Lifecycle

        /// <summary>
        /// 이벤트를 구독합니다.
        /// </summary>
        protected override void OnAttached()
        {
            if (string.IsNullOrEmpty(EventName) || AssociatedObject == null)
            {
                return;
            }

            _eventInfo = AssociatedObject.GetType().GetEvent(EventName);
            if (_eventInfo == null)
            {
                return;
            }

            var handlerType = _eventInfo.EventHandlerType;
            var invokeMethod = handlerType.GetMethod("Invoke");

            if (invokeMethod == null)
            {
                return;
            }

            _eventHandler = Delegate.CreateDelegate(
                handlerType,
                this,
                typeof(EventTrigger).GetMethod(nameof(OnEventFired), BindingFlags.NonPublic | BindingFlags.Instance));

            _eventInfo.AddEventHandler(AssociatedObject, _eventHandler);
        }

        /// <summary>
        /// 이벤트 구독을 해제합니다.
        /// </summary>
        protected override void OnDetaching()
        {
            if (_eventInfo != null && _eventHandler != null && AssociatedObject != null)
            {
                _eventInfo.RemoveEventHandler(AssociatedObject, _eventHandler);
            }

            _eventHandler = null;
            _eventInfo = null;
        }

        #endregion

        #region Private Methods

        private void OnEventFired(object sender, EventArgs args)
        {
            Action?.Execute(args);
        }

        #endregion
    }
}
