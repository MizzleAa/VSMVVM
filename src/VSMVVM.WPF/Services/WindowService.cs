using System;
using System.Windows;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// WPF 윈도우 서비스 구현체. View가 <see cref="Window"/>를 상속한 경우 그대로 띄우고,
    /// 그렇지 않으면 호스트 Window에 wrap합니다.
    /// 파라미터/결과는 ViewModel의 <c>DialogParameter</c>/<c>DialogResultData</c> 프로퍼티 reflection으로 처리합니다.
    /// </summary>
    public sealed class WindowService : IWindowService
    {
        #region IWindowService

        public DialogResult<TResult> ShowWindow<TResult>(string windowName, double width, double height)
        {
            return ShowWindow<TResult, object>(windowName, width, height, null);
        }

        public DialogResult<TResult> ShowWindow<TResult, TParam>(string windowName, double width, double height, TParam param)
        {
            var view = ResolveView(windowName);
            var window = view as Window ?? CreateHostWindow(view, width, height);

            if (view is Window directWindow)
            {
                directWindow.Width = width;
                directWindow.Height = height;
                if (directWindow.WindowStartupLocation == WindowStartupLocation.Manual)
                {
                    directWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                directWindow.Owner = GetActiveWindow();
            }

            // 파라미터 주입
            if (param != null)
            {
                SetDialogParameter(view, param);
            }

            // RequestClose 이벤트(있으면) 구독 — ViewModel이 닫기 요청 시 Window.Close()
            var dataContext = (view as FrameworkElement)?.DataContext;
            EventHandler closeHandler = null;
            var requestCloseEvent = dataContext?.GetType().GetEvent("RequestClose");
            if (requestCloseEvent != null)
            {
                closeHandler = (s, e) =>
                {
                    window.DialogResult = true;
                    window.Close();
                };
                requestCloseEvent.AddEventHandler(dataContext, closeHandler);
            }

            // 결과 회수
            var resultData = default(TResult);
            window.Closed += (sender, args) =>
            {
                resultData = GetDialogResultData<TResult>(view);
                if (closeHandler != null && requestCloseEvent != null)
                {
                    requestCloseEvent.RemoveEventHandler(dataContext, closeHandler);
                }
            };

            var wpfResult = window.ShowDialog();
            var dialogResult = wpfResult == true ? DialogResultType.OK : DialogResultType.Cancel;
            return new DialogResult<TResult>(dialogResult, resultData);
        }

        public void Show(string windowName, double width, double height)
        {
            var view = ResolveView(windowName);
            var window = view as Window ?? CreateHostWindow(view, width, height);

            if (view is Window directWindow)
            {
                directWindow.Width = width;
                directWindow.Height = height;
                if (directWindow.WindowStartupLocation == WindowStartupLocation.Manual)
                {
                    directWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                directWindow.Owner = GetActiveWindow();
            }

            window.Show();
        }

        #endregion

        #region Private Methods

        private static object ResolveView(string windowName)
        {
            var serviceProvider = ServiceLocator.GetServiceProvider();
            var view = serviceProvider.GetService(windowName);
            if (view == null)
            {
                throw new InvalidOperationException($"Window not found: {windowName}");
            }
            return view;
        }

        private static Window CreateHostWindow(object view, double width, double height)
        {
            return new Window
            {
                Content = view,
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = GetActiveWindow()
            };
        }

        private static void SetDialogParameter<TParam>(object view, TParam param)
        {
            var dataContext = (view as FrameworkElement)?.DataContext;
            if (dataContext == null)
            {
                return;
            }

            var paramProperty = dataContext.GetType().GetProperty("DialogParameter");
            if (paramProperty != null && paramProperty.CanWrite)
            {
                paramProperty.SetValue(dataContext, param);
            }
        }

        private static TResult GetDialogResultData<TResult>(object view)
        {
            var dataContext = (view as FrameworkElement)?.DataContext;
            if (dataContext == null)
            {
                return default;
            }

            var resultProperty = dataContext.GetType().GetProperty("DialogResultData");
            if (resultProperty != null)
            {
                var value = resultProperty.GetValue(dataContext);
                if (value is TResult typedValue)
                {
                    return typedValue;
                }
            }
            return default;
        }

        private static Window GetActiveWindow()
        {
            if (Application.Current?.MainWindow?.IsActive == true)
            {
                return Application.Current.MainWindow;
            }

            if (Application.Current?.Windows != null)
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.IsActive)
                    {
                        return window;
                    }
                }
            }

            return Application.Current?.MainWindow;
        }

        #endregion
    }
}
