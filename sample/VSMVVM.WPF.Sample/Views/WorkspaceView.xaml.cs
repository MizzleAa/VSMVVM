using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSMVVM.WPF.Sample.ViewModels;
using VSMVVM.WPF.Scheduler.Controls;
using VSMVVM.WPF.Scheduler.Services;
using VSMVVM.WPF.Scheduler.ViewModels;

namespace VSMVVM.WPF.Sample.Views
{
    /// <summary>
    /// 한 워크스페이스(탭)의 호스트 View. SchedulerDemoView 의 TabControl 컨텐츠로 사용.
    /// DataContext = <see cref="SampleWorkspaceViewModel"/>.
    /// </summary>
    public partial class WorkspaceView : UserControl
    {
        // 팔레트 드래그 상태 — MouseDown 후 임계값 넘는 MouseMove 가 오면 DoDragDrop 시작.
        private Point _paletteDragStart;
        private NodePaletteEntry _paletteDragSource;
        private const double PaletteDragThreshold = 5.0;

        public WorkspaceView()
        {
            InitializeComponent();
            // ImageView 노드 더블클릭 → 워크스페이스의 OpenImageViewForNode 위임.
            AddHandler(NodeView.NodeDoubleClickedEvent, new RoutedEventHandler(OnNodeDoubleClicked));
            // 캔버스에서 발화하는 PaletteEntryDropped 라우티드 이벤트 캐치 — 워크스페이스에 노드 추가 위임.
            AddHandler(NodePaletteDropRoutedEvents.PaletteEntryDroppedEvent,
                new NodePaletteDropEventHandler(OnPaletteEntryDropped));
            // 워크스페이스 단위 단축키 — F5(Run) / Shift+F5(Stop). 캔버스 focus 와 무관하게 동작.
            PreviewKeyDown += OnPreviewKeyDown;
        }

        // ============= 팔레트 드래그-앤-드롭 =============

        /// <summary>팔레트 항목 MouseDown — 드래그 시작 좌표/항목만 기록. 실제 DoDragDrop 은 MouseMove 에서 임계값 후.</summary>
        private void OnPaletteEntryMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el) return;
            if (el.DataContext is not NodePaletteEntry entry) return;
            _paletteDragSource = entry;
            _paletteDragStart = e.GetPosition(this);
        }

        /// <summary>임계값을 넘으면 DoDragDrop 시작. 캔버스가 받아 PaletteEntryDroppedEvent 발화.</summary>
        private void OnPaletteEntryMouseMove(object sender, MouseEventArgs e)
        {
            if (_paletteDragSource == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) { _paletteDragSource = null; return; }

            var current = e.GetPosition(this);
            var dx = current.X - _paletteDragStart.X;
            var dy = current.Y - _paletteDragStart.Y;
            if ((dx * dx + dy * dy) < PaletteDragThreshold * PaletteDragThreshold) return;

            if (sender is not FrameworkElement source) { _paletteDragSource = null; return; }
            var data = new DataObject(NodePaletteDragFormats.PaletteEntry, _paletteDragSource);
            try
            {
                DragDrop.DoDragDrop(source, data, DragDropEffects.Copy);
            }
            catch { /* 드래그 실패는 무시 */ }
            finally
            {
                _paletteDragSource = null;
            }
        }

        /// <summary>NodeGraphCanvas 가 발화한 라우티드 이벤트 — 활성 워크스페이스에 노드 추가 위임.</summary>
        private void OnPaletteEntryDropped(object sender, NodePaletteDropEventArgs e)
        {
            if (DataContext is not SampleWorkspaceViewModel ws) return;
            ws.AddNodeFromPaletteAt(e.Entry, e.CanvasPosition.X, e.CanvasPosition.Y);
            e.Handled = true;
        }

        private void OnNodeDoubleClicked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (e.OriginalSource is not NodeView nv) return;
            if (nv.DataContext is not NodeViewModel nvm) return;
            if (DataContext is not SampleWorkspaceViewModel ws) return;
            // ImageView 와 ChartView 노드 각각 자기 창을 오픈 (다른 노드면 no-op).
            ws.OpenImageViewForNode(nvm.Model);
            ws.OpenChartViewForNode(nvm.Model);
        }

        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DataContext is not SampleWorkspaceViewModel ws) return;
            // Ctrl 또는 Alt 가 같이 눌렸으면 컨테이너 단축키 우선 — 본 핸들러는 plain F5 / Shift+F5 만.
            if ((System.Windows.Input.Keyboard.Modifiers & (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt)) != 0) return;

            bool shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift;
            if (e.Key != System.Windows.Input.Key.F5) return;

            if (shift)
            {
                var stop = ws.GraphVm?.StopCommand;
                if (stop != null && stop.CanExecute(null))
                {
                    stop.Execute(null);
                    e.Handled = true;
                }
            }
            else
            {
                var run = ws.GraphVm?.RunCommand;
                if (run != null && run.CanExecute(null))
                {
                    run.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}
