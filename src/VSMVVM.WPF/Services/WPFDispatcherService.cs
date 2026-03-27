using System;
using System.Windows;
using System.Windows.Threading;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// WPF Dispatcher 래핑 서비스. Application.Current.Dispatcher를 통해 UI 스레드 안전성을 보장합니다.
    /// </summary>
    public sealed class WPFDispatcherService : IDispatcherService
    {
        #region IDispatcherService

        public void Invoke(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var dispatcher = GetDispatcher();

            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }

        public void InvokeAsync(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var dispatcher = GetDispatcher();
            dispatcher.InvokeAsync(action);
        }

        public bool CheckAccess()
        {
            return GetDispatcher().CheckAccess();
        }

        #endregion

        #region Private Methods

        private static Dispatcher GetDispatcher()
        {
            if (Application.Current != null)
            {
                return Application.Current.Dispatcher;
            }

            return Dispatcher.CurrentDispatcher;
        }

        #endregion
    }
}
