using System;
using System.Windows;
using System.Windows.Threading;

namespace VSMVVM.WPF.Extensions
{
    /// <summary>
    /// WPF 글로벌 예외 핸들링 확장.
    /// Application에 DispatcherUnhandledException, AppDomain.UnhandledException, TaskScheduler 예외를 등록합니다.
    /// </summary>
    public static class GlobalExceptionHandler
    {
        #region Fields

        private static Action<Exception> _handler;

        #endregion

        #region Public Methods

        /// <summary>
        /// 글로벌 예외 핸들러를 등록합니다.
        /// Application 시작 초기에 호출해야 합니다.
        /// </summary>
        public static void Register(Application application, Action<Exception> handler)
        {
            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _handler = handler;

            // WPF UI 스레드 예외
            application.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // 비-UI 스레드 예외
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

            // Task 미관찰 예외
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnTaskUnobservedException;
        }

        /// <summary>
        /// 글로벌 예외 핸들러를 해제합니다.
        /// </summary>
        public static void Unregister(Application application)
        {
            if (application == null)
            {
                return;
            }

            application.DispatcherUnhandledException -= OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException -= OnTaskUnobservedException;

            _handler = null;
        }

        #endregion

        #region Private Methods

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _handler?.Invoke(e.Exception);
            e.Handled = true;
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                _handler?.Invoke(exception);
            }
        }

        private static void OnTaskUnobservedException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            _handler?.Invoke(e.Exception);
            e.SetObserved();
        }

        #endregion
    }
}
