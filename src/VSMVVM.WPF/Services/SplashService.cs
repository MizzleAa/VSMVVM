using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// WPF 스플래시 윈도우 서비스 구현체.
    /// 별도 UI 스레드에서 스플래시 윈도우를 표시합니다.
    /// </summary>
    public sealed class SplashService : ISplashService
    {
        #region Fields

        private volatile Window _splashWindow;
        private volatile Dispatcher _splashDispatcher;

        #endregion

        #region Public Methods

        /// <summary>
        /// 스플래시 윈도우를 표시합니다. 별도 STA 스레드에서 실행됩니다.
        /// </summary>
        public void Show<TSplashWindow>() where TSplashWindow : Window, new()
        {
            var readyEvent = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    _splashWindow = new TSplashWindow();
                    _splashDispatcher = Dispatcher.CurrentDispatcher;

                    _splashWindow.Show();
                    readyEvent.Set();

                    Dispatcher.Run();
                }
                catch
                {
                    // 윈도우 생성 실패 시 대기를 해제합니다.
                    readyEvent.Set();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            readyEvent.Wait();
        }

        #endregion

        #region ISplashService

        public void Report(string message, double progress)
        {
            var dispatcher = _splashDispatcher;
            var window = _splashWindow;

            if (dispatcher == null || window == null)
            {
                return;
            }

            dispatcher.InvokeAsync(() =>
            {
                if (window.DataContext != null)
                {
                    var dc = window.DataContext;
                    var messageProp = dc.GetType().GetProperty("Message");
                    var progressProp = dc.GetType().GetProperty("Progress");

                    if (messageProp != null && messageProp.CanWrite)
                    {
                        messageProp.SetValue(dc, message);
                    }

                    if (progressProp != null && progressProp.CanWrite)
                    {
                        progressProp.SetValue(dc, progress);
                    }
                }
            });
        }

        public void Report(string message)
        {
            Report(message, -1.0);
        }

        public void Close()
        {
            var dispatcher = _splashDispatcher;
            var window = _splashWindow;

            _splashWindow = null;
            _splashDispatcher = null;

            if (dispatcher != null)
            {
                dispatcher.InvokeAsync(() =>
                {
                    window?.Close();
                    dispatcher.InvokeShutdown();
                });
            }
        }

        #endregion
    }
}
