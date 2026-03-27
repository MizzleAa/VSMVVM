using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 비동기 커맨드 인터페이스.
    /// </summary>
    public interface IAsyncRelayCommand : ICommand
    {
        /// <summary>
        /// 현재 비동기 작업이 실행 중인지 여부.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 비동기 실행.
        /// </summary>
        Task ExecuteAsync(object parameter);
    }
}
