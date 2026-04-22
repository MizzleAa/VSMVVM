using System;
using System.Threading;
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
        /// UI 스레드에서 비동기적으로 Action을 실행합니다.
        /// WPF: Dispatcher.InvokeAsync에 대응.
        /// </summary>
        public void InvokeAsync(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _syncContext.Post(_ => action(), null);
        }

        /// <summary>
        /// 현재 스레드가 UI 스레드인지 확인합니다.
        /// WPF: Dispatcher.CheckAccess()에 대응.
        /// </summary>
        public bool CheckAccess()
        {
            return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        #endregion
    }
}
