using System;
using System.Threading.Tasks;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// UI 스레드 안전성 보장 서비스 인터페이스.
    /// WPF: Application.Current.Dispatcher, WinForms: Control.Invoke 래핑.
    /// </summary>
    public interface IDispatcherService
    {
        /// <summary>
        /// UI 스레드에서 동기적으로 Action을 실행합니다.
        /// </summary>
        void Invoke(Action action);

        /// <summary>
        /// UI 스레드에서 비동기적으로 Action을 실행하고, 완료를 기다릴 수 있는 Task 를 반환합니다.
        /// fire-and-forget 호출은 반환값을 무시하면 되고, 완료까지 기다려야 한다면 await.
        /// </summary>
        Task InvokeAsync(Action action);

        /// <summary>
        /// 현재 스레드가 UI 스레드인지 확인합니다.
        /// </summary>
        bool CheckAccess();
    }
}
