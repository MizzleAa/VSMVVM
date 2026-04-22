using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VSMVVM.Core.MVVM;
using VSMVVM.WinForms.Design.Controls;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Services
{
    /// <summary>
    /// WinForms 다이얼로그 서비스 구현체.
    /// WPF DialogService와 동일한 IDialogService를 구현합니다.
    /// View/ViewModel 재사용, 버튼 프리셋, 파일/폴더 다이얼로그를 지원합니다.
    /// </summary>
    public sealed class WinFormsDialogService : IDialogService
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
                throw new InvalidOperationException($"View not found: {viewName}");

            var form = CreateDialogForm(view, (int)width, (int)height, buttons);
            var dialogResult = DialogResultType.None;

            // ViewModel에 파라미터 전달
            if (param != null)
            {
                SetDialogParameter(view, param);
            }

            // 다이얼로그 표시
            var winFormsResult = form.ShowDialog(GetActiveForm());

            // 결과 수집
            var resultData = GetDialogResultData<TResult>(view);

            if (winFormsResult == System.Windows.Forms.DialogResult.OK)
                dialogResult = DialogResultType.OK;
            else
                dialogResult = DialogResultType.Cancel;

            return new DialogResult<TResult>(dialogResult, resultData);
        }

        public void Show(string viewName, double width, double height)
        {
            var serviceProvider = ServiceLocator.GetServiceProvider();
            var view = serviceProvider.GetService(viewName);

            if (view == null)
                throw new InvalidOperationException($"View not found: {viewName}");

            var form = new Design.Controls.VSForm
            {
                Text = "",
                Width = (int)width,
                Height = (int)height,
                StartPosition = FormStartPosition.CenterScreen
            };

            if (view is Control control)
            {
                control.Dock = DockStyle.Fill;
                form.Padding = form.ContentPadding;
                form.Controls.Add(control);
            }

            form.Show(GetActiveForm());
        }

        public string[] OpenFileDialog(string initialDirectory, string title, string filter, bool multiselect = false)
        {
            using var dialog = new OpenFileDialog
            {
                InitialDirectory = initialDirectory ?? string.Empty,
                Title = title ?? string.Empty,
                Filter = filter ?? string.Empty,
                Multiselect = multiselect
            };

            var result = dialog.ShowDialog(GetActiveForm());
            if (result == System.Windows.Forms.DialogResult.OK)
                return dialog.FileNames;

            return Array.Empty<string>();
        }

        public string SaveFileDialog(string initialDirectory, string title, string filter)
        {
            using var dialog = new SaveFileDialog
            {
                InitialDirectory = initialDirectory ?? string.Empty,
                Title = title ?? string.Empty,
                Filter = filter ?? string.Empty
            };

            var result = dialog.ShowDialog(GetActiveForm());
            if (result == System.Windows.Forms.DialogResult.OK)
                return dialog.FileName;

            return null;
        }

        public string[] OpenFolderDialog(string initialDirectory, string title)
        {
            using var dialog = new FolderBrowserDialog
            {
                InitialDirectory = initialDirectory ?? string.Empty,
                Description = title ?? string.Empty,
                UseDescriptionForTitle = true
            };

            var result = dialog.ShowDialog(GetActiveForm());
            if (result == System.Windows.Forms.DialogResult.OK)
                return new[] { dialog.SelectedPath };

            return Array.Empty<string>();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 다이얼로그 Form을 생성합니다.
        /// WPF DialogService.CreateDialogWindow에 대응합니다.
        /// Design 시스템의 VSForm을 사용하여 일관된 디자인을 유지합니다.
        /// </summary>
        private static VSForm CreateDialogForm(object view, int width, int height, DialogButtons buttons)
        {
            var form = new VSForm
            {
                Text = "",
                Width = width,
                Height = height,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.None,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var theme = ThemeManager.Current;

            // 콘텐츠 패널
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = theme.BgPrimary
            };

            if (view is Control control)
            {
                control.Dock = DockStyle.Fill;
                contentPanel.Controls.Add(control);
            }

            // 버튼 패널
            var buttonPanel = CreateButtonPanel(form, buttons);

            form.Padding = form.ContentPadding;
            form.Controls.Add(contentPanel);
            form.Controls.Add(buttonPanel);

            return form;
        }

        /// <summary>
        /// 다이얼로그 하단 버튼 패널을 생성합니다.
        /// WPF DialogService.CreateButtonPanel에 대응합니다.
        /// </summary>
        private static Panel CreateButtonPanel(Form dialogForm, DialogButtons buttons)
        {
            var theme = ThemeManager.Current;

            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = theme.BgSecondary,
                Padding = new System.Windows.Forms.Padding(16, 8, 16, 8)
            };

            // 오른쪽 정렬 FlowLayoutPanel
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                BackColor = theme.BgSecondary
            };

            switch (buttons)
            {
                case DialogButtons.OK:
                    flow.Controls.Add(CreateDialogButton(dialogForm, "OK", true));
                    break;

                case DialogButtons.OKCancel:
                    flow.Controls.Add(CreateDialogButton(dialogForm, "Cancel", false));
                    flow.Controls.Add(CreateDialogButton(dialogForm, "OK", true));
                    break;

                case DialogButtons.YesNo:
                    flow.Controls.Add(CreateDialogButton(dialogForm, "No", false));
                    flow.Controls.Add(CreateDialogButton(dialogForm, "Yes", true));
                    break;

                case DialogButtons.YesNoCancel:
                    flow.Controls.Add(CreateDialogButton(dialogForm, "Cancel", false));
                    flow.Controls.Add(CreateDialogButton(dialogForm, "No", false));
                    flow.Controls.Add(CreateDialogButton(dialogForm, "Yes", true));
                    break;
            }

            panel.Controls.Add(flow);
            return panel;
        }

        /// <summary>
        /// 다이얼로그 버튼을 생성합니다.
        /// WPF DialogService.CreateDialogButton에 대응합니다.
        /// Design 시스템의 VSButton을 사용합니다.
        /// </summary>
        private static VSButton CreateDialogButton(Form dialogForm, string text, bool isPrimary)
        {
            var button = new VSButton
            {
                Text = text,
                Variant = isPrimary ? ButtonVariant.Primary : ButtonVariant.Secondary,
                Size = new Size(80, 32),
                Margin = new System.Windows.Forms.Padding(4, 0, 0, 0)
            };

            button.Click += (sender, args) =>
            {
                dialogForm.DialogResult = isPrimary
                    ? System.Windows.Forms.DialogResult.OK
                    : System.Windows.Forms.DialogResult.Cancel;
                dialogForm.Close();
            };

            return button;
        }

        /// <summary>
        /// ViewModel에 DialogParameter를 설정합니다.
        /// WPF DialogService.SetDialogParameter에 대응합니다.
        /// </summary>
        private static void SetDialogParameter<TParam>(object view, TParam param)
        {
            if (view is Control control)
            {
                var dataContext = control.GetType().GetProperty("DataContext")?.GetValue(control);
                if (dataContext != null)
                {
                    var paramProperty = dataContext.GetType().GetProperty("DialogParameter");
                    if (paramProperty != null && paramProperty.CanWrite)
                    {
                        paramProperty.SetValue(dataContext, param);
                    }
                }
            }
        }

        /// <summary>
        /// ViewModel에서 DialogResultData를 가져옵니다.
        /// WPF DialogService.GetDialogResultData에 대응합니다.
        /// </summary>
        private static TResult GetDialogResultData<TResult>(object view)
        {
            if (view is Control control)
            {
                var dataContext = control.GetType().GetProperty("DataContext")?.GetValue(control);
                if (dataContext != null)
                {
                    var resultProperty = dataContext.GetType().GetProperty("DialogResultData");
                    if (resultProperty != null)
                    {
                        var value = resultProperty.GetValue(dataContext);
                        if (value is TResult typedValue)
                            return typedValue;
                    }
                }
            }

            return default;
        }

        /// <summary>
        /// 현재 활성 Form을 가져옵니다.
        /// WPF DialogService.GetActiveWindow()에 대응합니다.
        /// </summary>
        private static Form GetActiveForm()
        {
            return Form.ActiveForm;
        }

        #endregion
    }
}
