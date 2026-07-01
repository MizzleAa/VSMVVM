using System.Windows.Controls;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>
    /// 선택된 NodeViewModel 의 마지막 실행 시 입출력 데이터 핀 값을 표시.
    /// DataContext 는 NodeGraphViewModel.SelectedNode (NodeViewModel) 로 직접 바인딩한다.
    /// 선택이 null 이면 placeholder, 실행 전이면 "(실행되지 않음)" 안내.
    /// </summary>
    public partial class NodeInspectorPanel : UserControl
    {
        public NodeInspectorPanel()
        {
            InitializeComponent();
        }
    }
}
