using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using VSMVVM.WPF.Behaviors.Base;

namespace VSMVVM.WPF.Behaviors.Behaviors
{
    /// <summary>
    /// 이벤트 발생 시 ICommand를 호출하는 Behavior.
    /// EventName으로 지정된 이벤트 발생 시 Command를 실행합니다.
    /// </summary>
    public sealed class EventToCommandBehavior : Behavior<FrameworkElement>
    {
        #region DependencyProperties

        /// <summary>
        /// 감시할 이벤트 이름 종속성 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty EventNameProperty =
            DependencyProperty.Register(
                nameof(EventName),
                typeof(string),
                typeof(EventToCommandBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// 실행할 Command 종속성 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(
                nameof(Command),
                typeof(ICommand),
                typeof(EventToCommandBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Command 파라미터 종속성 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register(
                nameof(CommandParameter),
                typeof(object),
                typeof(EventToCommandBehavior),
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
        /// 실행할 Command.
        /// </summary>
        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        /// <summary>
        /// Command에 전달할 파라미터.
        /// </summary>
        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
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

            _eventHandler = Delegate.CreateDelegate(
                _eventInfo.EventHandlerType,
                this,
                typeof(EventToCommandBehavior).GetMethod(nameof(OnEventFired), BindingFlags.NonPublic | BindingFlags.Instance));

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
            var command = Command;
            if (command == null)
            {
                return;
            }

            var parameter = CommandParameter ?? args;

            if (command.CanExecute(parameter))
            {
                command.Execute(parameter);
            }
        }

        #endregion
    }
}
