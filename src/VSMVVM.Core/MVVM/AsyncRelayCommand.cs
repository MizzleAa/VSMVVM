using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 비동기 커맨드 구현. IsRunning 프로퍼티 + CanExecute 지원.
    /// 실행 중에는 자동으로 CanExecute=false 처리.
    /// </summary>
    public sealed class AsyncRelayCommand : IAsyncRelayCommand, INotifyPropertyChanged
    {
        #region Fields

        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private bool _isRunning;

        #endregion

        #region Constructors

        public AsyncRelayCommand(Func<Task> execute) : this(execute, null)
        {
        }

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IAsyncRelayCommand

        /// <summary>
        /// 현재 비동기 작업 실행 여부.
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (_isRunning == value) return;
                _isRunning = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }

        public async Task ExecuteAsync(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            IsRunning = true;
            try
            {
                await _execute();
            }
            finally
            {
                IsRunning = false;
            }
        }

        #endregion

        #region ICommand

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            if (_isRunning)
                return false;

            return _canExecute == null || _canExecute();
        }

        public async void Execute(object parameter)
        {
            await ExecuteAsync(parameter);
        }

        #endregion

        #region Public Methods

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }

    /// <summary>
    /// 제네릭 비동기 커맨드 구현. IsRunning + CanExecute 지원.
    /// </summary>
    public sealed class AsyncRelayCommand<T> : IAsyncRelayCommand, INotifyPropertyChanged
    {
        #region Fields

        private readonly Func<T, Task> _execute;
        private readonly Func<T, bool> _canExecute;
        private bool _isRunning;

        #endregion

        #region Constructors

        public AsyncRelayCommand(Func<T, Task> execute) : this(execute, null)
        {
        }

        public AsyncRelayCommand(Func<T, Task> execute, Func<T, bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IAsyncRelayCommand

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (_isRunning == value) return;
                _isRunning = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }

        public async Task ExecuteAsync(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            IsRunning = true;
            try
            {
                await _execute((T)parameter);
            }
            finally
            {
                IsRunning = false;
            }
        }

        #endregion

        #region ICommand

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            if (_isRunning)
                return false;

            if (parameter == null && default(T) != null)
                return false;

            return _canExecute == null || _canExecute((T)parameter);
        }

        public async void Execute(object parameter)
        {
            await ExecuteAsync(parameter);
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
