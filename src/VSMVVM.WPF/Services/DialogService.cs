using System;
using System.Windows;
using System.Windows.Controls;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// WPF 다이얼로그 서비스 구현체.
    /// View/ViewModel 재사용, 버튼 프리셋, 파일/폴더 다이얼로그를 지원합니다.
    /// Design 시스템의 VSMVVMDialogWindowStyle 및 DialogButton 스타일을 자동 적용합니다.
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        #region IDialogService

        public DialogResult<TResult> ShowDialog<TResult>(string viewName, double width, double height, DialogButtons buttons = DialogButtons.OKCancel)
        {
            return ShowDialog<TResult, object>(viewName, width, height, null, buttons);
        }

        public DialogResult<TResult> ShowDialog<TResult, TParam>(string viewName, double width, double height, TParam param, DialogButtons buttons = DialogButtons.OKCancel)
        {
            var serviceProvider = ServiceLocator.GetServiceProvider();
            var view = serviceProvider.GetService(viewName);

            if (view == null)
            {
                throw new InvalidOperationException($"View not found: {viewName}");
            }

            var window = CreateDialogWindow(view, width, height, buttons);
            var dialogResult = DialogResultType.None;

            // ViewModel에 파라미터 전달
            if (param != null)
            {
                SetDialogParameter(view, param);
            }

            // 다이얼로그 결과 수집
            var resultData = default(TResult);

            window.Closed += (sender, args) =>
            {
                resultData = GetDialogResultData<TResult>(view);
                // ViewModel 이 IDisposable 이면 Subscriptions 자동 정리.
                var dataContext = (view as System.Windows.FrameworkElement)?.DataContext;
                if (dataContext is System.IDisposable disposable)
                {
                    try { disposable.Dispose(); } catch { }
                }
            };

            var wpfResult = window.ShowDialog();

            if (wpfResult == true)
            {
                dialogResult = DialogResultType.OK;
            }
            else
            {
                dialogResult = DialogResultType.Cancel;
            }

            return new DialogResult<TResult>(dialogResult, resultData);
        }

        public void Show(string viewName, double width, double height)
        {
            var serviceProvider = ServiceLocator.GetServiceProvider();
            var view = serviceProvider.GetService(viewName);

            if (view == null)
            {
                throw new InvalidOperationException($"View not found: {viewName}");
            }

            var window = new Window
            {
                Content = view,
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Owner = GetActiveWindow()
            };

            ApplyDialogWindowStyle(window);
            window.Show();
        }

        public string[] OpenFileDialog(string initialDirectory, string title, string filter, bool multiselect = false)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                InitialDirectory = initialDirectory ?? string.Empty,
                Title = title ?? string.Empty,
                Filter = filter ?? string.Empty,
                Multiselect = multiselect
            };

            var result = dialog.ShowDialog(GetActiveWindow());
            if (result == true)
            {
                return dialog.FileNames;
            }

            return Array.Empty<string>();
        }

        public string SaveFileDialog(string initialDirectory, string title, string filter)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = initialDirectory ?? string.Empty,
                Title = title ?? string.Empty,
                Filter = filter ?? string.Empty
            };

            var result = dialog.ShowDialog(GetActiveWindow());
            if (result == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        public string[] OpenFolderDialog(string initialDirectory, string title)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                InitialDirectory = initialDirectory ?? string.Empty,
                Title = title ?? string.Empty,
                Multiselect = false
            };

            var result = dialog.ShowDialog(GetActiveWindow());
            if (result == true)
            {
                return new[] { dialog.FolderName };
            }

            return Array.Empty<string>();
        }

        #endregion

        #region Private Methods

        private static Window CreateDialogWindow(object view, double width, double height, DialogButtons buttons)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 컨텐츠 영역
            if (view is UIElement uiElement)
            {
                Grid.SetRow(uiElement, 0);
                grid.Children.Add(uiElement);
            }

            // 버튼 영역 (디자인 시스템 스타일 적용)
            var buttonPanel = CreateButtonPanel(buttons);
            Grid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            var window = new Window
            {
                Content = grid,
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Owner = GetActiveWindow(),
                ResizeMode = System.Windows.ResizeMode.NoResize
            };

            ApplyDialogWindowStyle(window);
            return window;
        }

        private static Border CreateButtonPanel(DialogButtons buttons)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0)
            };

            switch (buttons)
            {
                case DialogButtons.OK:
                    panel.Children.Add(CreateDialogButton("OK", true));
                    break;

                case DialogButtons.OKCancel:
                    panel.Children.Add(CreateDialogButton("Cancel", false));
                    panel.Children.Add(CreateDialogButton("OK", true));
                    break;

                case DialogButtons.YesNo:
                    panel.Children.Add(CreateDialogButton("No", false));
                    panel.Children.Add(CreateDialogButton("Yes", true));
                    break;

                case DialogButtons.YesNoCancel:
                    panel.Children.Add(CreateDialogButton("Cancel", false));
                    panel.Children.Add(CreateDialogButton("No", false));
                    panel.Children.Add(CreateDialogButton("Yes", true));
                    break;
            }

            // 버튼 패널을 감싸는 Border (상단 구분선 포함)
            var border = new Border
            {
                BorderBrush = Application.Current?.TryFindResource("BorderMuted") as System.Windows.Media.Brush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 12, 16, 12),
                Child = panel
            };

            return border;
        }

        private static Button CreateDialogButton(string text, bool isPrimary)
        {
            var button = new Button
            {
                Content = text,
                Margin = new Thickness(4, 0, 0, 0),
                IsDefault = isPrimary,
                IsCancel = !isPrimary
            };

            // Design 시스템 버튼 스타일 적용
            try
            {
                var styleKey = isPrimary ? "DialogButtonPrimary" : "DialogButtonSecondary";
                var style = Application.Current?.TryFindResource(styleKey) as Style;
                if (style != null)
                {
                    button.Style = style;
                }
            }
            catch
            {
                // 스타일을 찾지 못하면 기본 스타일 유지
            }

            button.Click += (sender, args) =>
            {
                var win = Window.GetWindow((Button)sender);
                if (win != null)
                {
                    win.DialogResult = isPrimary;
                    win.Close();
                }
            };

            return button;
        }

        private static void SetDialogParameter<TParam>(object view, TParam param)
        {
            if (view is FrameworkElement fe && fe.DataContext != null)
            {
                var paramProperty = fe.DataContext.GetType().GetProperty("DialogParameter");
                if (paramProperty != null && paramProperty.CanWrite)
                {
                    paramProperty.SetValue(fe.DataContext, param);
                }
            }
        }

        private static TResult GetDialogResultData<TResult>(object view)
        {
            if (view is FrameworkElement fe && fe.DataContext != null)
            {
                var resultProperty = fe.DataContext.GetType().GetProperty("DialogResultData");
                if (resultProperty != null)
                {
                    var value = resultProperty.GetValue(fe.DataContext);
                    if (value is TResult typedValue)
                    {
                        return typedValue;
                    }
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

        /// <summary>
        /// Design 시스템의 Dialog Window 스타일을 적용합니다.
        /// VSMVVMDialogWindowStyle → VSMVVMWindowStyle 순서로 fallback합니다.
        /// </summary>
        private static void ApplyDialogWindowStyle(Window window)
        {
            try
            {
                var dialogStyle = Application.Current?.TryFindResource("VSMVVMDialogWindowStyle") as Style;
                if (dialogStyle != null)
                {
                    window.Style = dialogStyle;
                    return;
                }

                // Fallback: 일반 Window 스타일
                var windowStyle = Application.Current?.TryFindResource("VSMVVMWindowStyle") as Style;
                if (windowStyle != null)
                {
                    window.Style = windowStyle;
                }
            }
            catch
            {
                // 리소스를 찾지 못하면 기본 스타일 유지
            }
        }

        #endregion
    }
}
