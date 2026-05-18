using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WinForms.Services
{
    /// <summary>
    /// WinForms Dispatcher 서비스. Control.Invoke/BeginInvoke를 통해 UI 스레드 안전성을 보장합니다.
    /// WPF WPFDispatcherService(Application.Current.Dispatcher 기반)에 대응합니다.
    /// </summary>
    public sealed class WinFormsDispatcherService : IDispatcherService
    {
        #region Fields

        private readonly SynchronizationContext _syncContext;
        private readonly int _mainThreadId;

        #endregion

        #region Constructor

        /// <summary>
        /// 현재 SynchronizationContext를 캡처하여 UI 스레드를 기억합니다.
        /// Application.Run() 전에 생성해야 합니다.
        /// </summary>
        public WinFormsDispatcherService()
        {
            _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        #endregion

        #region IDispatcherService

        /// <summary>
        /// UI 스레드에서 동기적으로 Action을 실행합니다.
        /// WPF: Dispatcher.Invoke에 대응.
        /// </summary>
        public void Invoke(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (CheckAccess())
            {
                action();
            }
            else
            {
                _syncContext.Send(_ => action(), null);
            }
        }

        /// <summary>
        /// UI 스레드에서 비동기적으로 Action을 실행하고, 완료를 기다릴 수 있는 Task 를 반환합니다.
        /// WPF: Dispatcher.InvokeAsync(action).Task 에 대응.
        /// </summary>
        public Task InvokeAsync(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _syncContext.Post(_ =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }, null);
            return tcs.Task;
        }

        /// <summary>
        /// 현재 스레드가 UI 스레드인지 확인합니다.
        /// WPF: Dispatcher.CheckAccess()에 대응.
        /// </summary>
        public bool CheckAccess()
        {
            return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        /// <summary>
        /// UI 스레드 메시지 큐를 한 사이클 양보합니다.
        /// WinForms 는 DispatcherPriority 개념 없음 — SynchronizationContext.Post 로 큐 끝에 빈 작업 post.
        /// WPF Yield(Background) 의 frame 양보와 의도 동등.
        /// </summary>
        public Task Yield()
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _syncContext.Post(_ => tcs.TrySetResult(null), null);
            return tcs.Task;
        }

        #endregion
    }
}
