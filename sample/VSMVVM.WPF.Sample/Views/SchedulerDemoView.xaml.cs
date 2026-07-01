using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSMVVM.WPF.Sample.ViewModels;

namespace VSMVVM.WPF.Sample.Views
{
    /// <summary>
    /// 멀티 탭 컨테이너 View. 본문 그래프/팔레트/로그 패널은 자식 WorkspaceView 가 담당.
    /// 본 View 는 컨테이너 액션바 + TabControl + 컨테이너 단축키만 호스팅.
    /// </summary>
    public partial class SchedulerDemoView : UserControl
    {
        public SchedulerDemoView()
        {
            InitializeComponent();
            Focusable = true;
            // 컨테이너 단축키는 코드비하인드 KeyDown 에서 처리 — XAML 의 InputBindings 가
            // 자식 컨트롤(노드 캔버스 등) 의 focus 가 있을 때 안 닿는 경우가 있어 안정적 경로 선택.
            PreviewKeyDown += OnPreviewKeyDown;
        }

        /// <summary>
        /// 컨테이너 단축키:
        ///   • Ctrl+T              → New Workspace
        ///   • Ctrl+W              → Close Active Workspace
        ///   • Ctrl+Shift+F5       → Run All Workspaces
        /// 개별 워크스페이스 Run/Stop 단축키(F5, Shift+F5) 는 NodeGraphCanvas 가 자체 키 처리 — 본 컨테이너에서 안 잡음.
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not SchedulerDemoViewModel vm) return;

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (ctrl && !shift && e.Key == Key.T)
            {
                if (vm.NewWorkspaceCommand?.CanExecute(null) == true)
                {
                    vm.NewWorkspaceCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }
            if (ctrl && !shift && e.Key == Key.W)
            {
                var ws = vm.ActiveWorkspace;
                if (ws != null && vm.CloseWorkspaceCommand?.CanExecute(ws) == true)
                {
                    vm.CloseWorkspaceCommand.Execute(ws);
                    e.Handled = true;
                }
                return;
            }
            if (ctrl && shift && e.Key == Key.F5)
            {
                if (vm.RunAllWorkspacesCommand?.CanExecute(null) == true)
                {
                    vm.RunAllWorkspacesCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }
        }
    }
}
