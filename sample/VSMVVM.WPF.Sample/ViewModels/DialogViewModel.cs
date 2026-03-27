using System.Windows;
using System.Windows.Controls;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// ViewModel for IDialogService demo.
    /// Design 시스템의 VSMVVMDialogWindowStyle 다이얼로그를 시연합니다.
    /// </summary>
    public partial class DialogViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;

        [Property]
        private string _dialogResult = "No dialog shown yet.";


        public DialogViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        [RelayCommand]
        private void ShowMessage()
        {
            ShowDesignDialog(
                "Information",
                "This is a message from VSMVVM!\nDesign system dialog style is applied.",
                DialogButtons.OK);
            DialogResult = "Message dialog closed.";
        }

        [RelayCommand]
        private void ShowConfirm()
        {
            var result = ShowDesignDialog(
                "Confirm",
                "Do you want to continue?\nThis demonstrates Yes/No button presets.",
                DialogButtons.YesNo);
            DialogResult = result == true ? "User confirmed: Yes" : "User confirmed: No";
        }

        [RelayCommand]
        private void ShowOKCancel()
        {
            var result = ShowDesignDialog(
                "OK/Cancel",
                "OK / Cancel button preset demo.\nPrimary and Secondary button styles are applied.",
                DialogButtons.OKCancel);
            DialogResult = result == true ? "User clicked: OK" : "User clicked: Cancel";
        }

        /// <summary>
        /// Design 시스템 스타일이 적용된 다이얼로그를 직접 생성합니다.
        /// DialogService.ShowDialog 사용 시에도 동일한 스타일이 자동 적용됩니다.
        /// </summary>
        private bool? ShowDesignDialog(string title, string message, DialogButtons buttons)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 컨텐츠 영역
            var contentPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(24, 20, 24, 20)
            };

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Application.Current.TryFindResource("TextPrimary") as System.Windows.Media.Brush,
                FontSize = 14,
                LineHeight = 22
            };
            contentPanel.Children.Add(messageText);

            Grid.SetRow(contentPanel, 0);
            grid.Children.Add(contentPanel);

            // 버튼 영역
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var buttonBorder = new Border
            {
                BorderBrush = Application.Current.TryFindResource("BorderMuted") as System.Windows.Media.Brush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 12, 16, 12),
                Child = buttonPanel
            };

            void AddButton(string text, bool isPrimary, bool? result)
            {
                var btn = new Button
                {
                    Content = text,
                    Margin = new Thickness(4, 0, 0, 0),
                    IsDefault = isPrimary,
                    IsCancel = !isPrimary
                };

                var styleKey = isPrimary ? "DialogButtonPrimary" : "DialogButtonSecondary";
                var style = Application.Current.TryFindResource(styleKey) as Style;
                if (style != null) btn.Style = style;

                btn.Click += (s, e) =>
                {
                    var win = Window.GetWindow((Button)s);
                    if (win != null)
                    {
                        win.DialogResult = result;
                        win.Close();
                    }
                };
                buttonPanel.Children.Add(btn);
            }

            switch (buttons)
            {
                case DialogButtons.OK:
                    AddButton("OK", true, true);
                    break;
                case DialogButtons.OKCancel:
                    AddButton("Cancel", false, false);
                    AddButton("OK", true, true);
                    break;
                case DialogButtons.YesNo:
                    AddButton("No", false, false);
                    AddButton("Yes", true, true);
                    break;
                case DialogButtons.YesNoCancel:
                    AddButton("Cancel", false, null);
                    AddButton("No", false, false);
                    AddButton("Yes", true, true);
                    break;
            }

            Grid.SetRow(buttonBorder, 1);
            grid.Children.Add(buttonBorder);

            var window = new Window
            {
                Title = title,
                Content = grid,
                Width = 420,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = GetActiveWindow(),
                ResizeMode = ResizeMode.NoResize
            };

            // Design 시스템 Dialog 스타일 적용
            var dialogStyle = Application.Current.TryFindResource("VSMVVMDialogWindowStyle") as Style;
            if (dialogStyle != null)
            {
                window.Style = dialogStyle;
            }

            return window.ShowDialog();
        }

        private static Window GetActiveWindow()
        {
            if (Application.Current?.MainWindow?.IsActive == true)
                return Application.Current.MainWindow;

            if (Application.Current?.Windows != null)
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.IsActive)
                        return window;
                }
            }

            return Application.Current?.MainWindow;
        }
    }
}
