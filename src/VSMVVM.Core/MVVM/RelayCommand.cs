using System;
using System.Windows.Input;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 동기 커맨드 구현. CanExecute 지원.
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        #region Fields

        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        #endregion

        #region Constructors

        public RelayCommand(Action execute) : this(execute, null)
        {
        }

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        #endregion

        #region ICommand

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                _execute();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// CanExecute 상태 변경을 UI에 알립니다.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }

    /// <summary>
    /// 제네릭 동기 커맨드 구현. CanExecute 지원.
    /// </summary>
    public sealed class RelayCommand<T> : ICommand
    {
        #region Fields

        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        #endregion

        #region Constructors

        public RelayCommand(Action<T> execute) : this(execute, null)
        {
        }

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        #endregion

        #region ICommand

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            if (parameter == null && default(T) != null)
                return false;

            return _canExecute == null || _canExecute((T)parameter);
        }

        public void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                _execute((T)parameter);
            }
        }

        #endregion

        #region Public Methods

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
