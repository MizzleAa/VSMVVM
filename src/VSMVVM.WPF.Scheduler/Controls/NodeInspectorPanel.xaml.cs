using System.Windows;
using System.Windows.Controls;
using VSMVVM.WPF.Scheduler.ViewModels;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>
    /// 선택된 NodeViewModel 의 마지막 실행 시 입출력 데이터 핀 값을 표시.
    /// DataContext 는 NodeGraphViewModel.SelectedNode (NodeViewModel) 로 직접 바인딩한다.
    /// 선택이 null 이면 placeholder, 실행 전이면 "(실행되지 않음)" 안내.
    /// </summary>
    public partial class NodeInspectorPanel : UserControl
    {
        private CollectionDetailWindow _detailWindow;

        public NodeInspectorPanel()
        {
            InitializeComponent();
            Unloaded += (_, _) => CloseDetailWindow();
        }

        /// <summary>
        /// PinSnapshotRowTemplate 의 "자세히" 버튼 클릭. Button.DataContext 는 <see cref="PinValueSnapshot"/>.
        /// 이미 창이 열려 있으면 DataContext 만 갈아끼고 앞으로 가져와, 다중 창을 만들지 않는다.
        /// </summary>
        private void OnPinDetailClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe) || !(fe.DataContext is PinValueSnapshot snapshot))
                return;

            if (_detailWindow == null)
            {
                _detailWindow = new CollectionDetailWindow
                {
                    Owner = Window.GetWindow(this),
                };
                _detailWindow.Closed += (_, _) => _detailWindow = null;
            }

            _detailWindow.DataContext = new CollectionDetailViewModel(snapshot.DisplayName, snapshot.Value);
            if (!_detailWindow.IsVisible) _detailWindow.Show();
            _detailWindow.Activate();
        }

        private void CloseDetailWindow()
        {
            var w = _detailWindow;
            _detailWindow = null;
            w?.Close();
        }

        /// <summary>헤더 "Open Code" 버튼 — DataContext 의 NodeViewModel 에 소스 오픈 요청을 발화.
        /// 실제 스니펫 로딩/에디터 오픈은 호스트(예: SampleWorkspaceViewModel)가 이벤트 핸들러에서 처리.</summary>
        private void OnOpenCodeClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is NodeViewModel nvm) nvm.RaiseOpenSourceRequested();
        }
    }
}
