using System.Windows.Controls;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>
    /// 노드 그래프 에디터 툴바. DataContext는 NodeGraphViewModel.
    /// 보유 버튼: Run / Stop / Continue / Step Over / Toggle Breakpoint.
    /// Visibility 토글로 Run ↔ Stop, Continue/Step ↔ 숨김을 IsRunning/IsPaused 상태에 따라 자동 전환.
    /// </summary>
    public partial class NodeGraphToolbar : UserControl
    {
        public NodeGraphToolbar()
        {
            InitializeComponent();
        }
    }
}
