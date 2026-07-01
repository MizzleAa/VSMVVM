using System;
using System.Threading;
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
        // Command 생성 시점의 SynchronizationContext (보통 UI 스레드) — RaiseCanExecuteChanged 마샬링용.
        private readonly SynchronizationContext _capturedContext;

        #endregion

        #region Constructors

        public RelayCommand(Action execute) : this(execute, null)
        {
        }

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _capturedContext = SynchronizationContext.Current;
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
        /// CanExecute 상태 변경을 UI에 알립니다. 백그라운드 스레드에서 호출되어도 captured
        /// <see cref="SynchronizationContext"/> 로 마샬링되어 WPF dispatcher access check 통과.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandThreadMarshal.Invoke(_capturedContext, CanExecuteChanged, this);
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
        private readonly SynchronizationContext _capturedContext;

        #endregion

        #region Constructors

        public RelayCommand(Action<T> execute) : this(execute, null)
        {
        }

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _capturedContext = SynchronizationContext.Current;
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
            CommandThreadMarshal.Invoke(_capturedContext, CanExecuteChanged, this);
        }

        #endregion
    }
}
