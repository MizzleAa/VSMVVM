using System;
using System.Threading.Tasks;
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

        public Task InvokeAsync(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var dispatcher = GetDispatcher();
            return dispatcher.InvokeAsync(action).Task;
        }

        public bool CheckAccess()
        {
            return GetDispatcher().CheckAccess();
        }

        public Task Yield()
        {
            // Background priority — WPF Render priority 보다 낮아 우리 빈 람다가 처리될 때쯤이면
            // layout/render pass 가 이미 끝나 IsLoading=true 등이 화면에 그려진 상태가 됨.
            // Normal priority 로 post 하면 Render 보다 먼저 처리되어 frame 양보 효과가 사라진다.
            return GetDispatcher().InvokeAsync(() => { }, DispatcherPriority.Background).Task;
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
