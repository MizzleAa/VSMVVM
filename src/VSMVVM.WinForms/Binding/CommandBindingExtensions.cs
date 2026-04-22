using System;
using System.Windows.Forms;
using System.Windows.Input;

namespace VSMVVM.WinForms.Binding
{
    /// <summary>
    /// ICommand → Control.Click 바인딩 확장 메서드.
    /// CanExecute에 따라 Enabled 자동 갱신. Dispose 시 양쪽 해제.
    /// </summary>
    public static class CommandBindingExtensions
    {
        /// <summary>
        /// 기본 Click 이벤트 기반 바인딩. Button/Label 등 모든 Control 대상.
        /// </summary>
        public static BindingHandle BindCommand(this Control control, ICommand command, object parameter = null)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (command == null) throw new ArgumentNullException(nameof(command));

            EventHandler onClick = (s, e) =>
            {
                if (command.CanExecute(parameter))
                    command.Execute(parameter);
            };
            control.Click += onClick;

            EventHandler onCec = (s, e) =>
            {
                if (control.InvokeRequired)
                {
                    try { control.BeginInvoke((Action)(() => control.Enabled = command.CanExecute(parameter))); }
                    catch (ObjectDisposedException) { }
                    catch (InvalidOperationException) { }
                }
                else
                {
                    control.Enabled = command.CanExecute(parameter);
                }
            };
            command.CanExecuteChanged += onCec;

            control.Enabled = command.CanExecute(parameter);

            return new BindingHandle(control, () =>
            {
                control.Click -= onClick;
                command.CanExecuteChanged -= onCec;
            });
        }
    }
}
