using System.Windows;
using System.Windows.Input;
using VSMVVM.WPF.Behaviors.Base;

namespace VSMVVM.WPF.Behaviors.Actions
{
    /// <summary>
    /// 트리거 발동 시 ICommand를 호출하는 TriggerAction.
    /// </summary>
    public sealed class InvokeCommandAction : TriggerAction<DependencyObject>
    {
        #region DependencyProperties

        /// <summary>
        /// 실행할 Command 종속성 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(
                nameof(Command),
                typeof(ICommand),
                typeof(InvokeCommandAction),
                new PropertyMetadata(null));

        /// <summary>
        /// Command에 전달할 파라미터 종속성 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register(
                nameof(CommandParameter),
                typeof(object),
                typeof(InvokeCommandAction),
                new PropertyMetadata(null));

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

        #region Override

        /// <summary>
        /// Command를 실행합니다.
        /// </summary>
        protected override void Invoke(object parameter)
        {
            var command = Command;
            if (command == null)
            {
                return;
            }

            var commandParameter = CommandParameter ?? parameter;

            if (command.CanExecute(commandParameter))
            {
                command.Execute(commandParameter);
            }
        }

        #endregion
    }
}
