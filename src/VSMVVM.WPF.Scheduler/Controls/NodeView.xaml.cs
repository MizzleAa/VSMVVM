using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VSMVVM.WPF.Scheduler.ViewModels;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>
    /// 한 노드의 시각 표현. NodeGraphCanvas의 _nodeLayer에 호스팅된다.
    /// DataContext는 NodeViewModel.
    /// 헤더 드래그로 노드 이동, 선택 시 IsSelected 토글.
    /// </summary>
    public partial class NodeView : UserControl
    {
        public static readonly DependencyProperty IsExecutingProperty =
            DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(NodeView),
                new PropertyMetadata(false));

        public bool IsExecuting
        {
            get => (bool)GetValue(IsExecutingProperty);
            set => SetValue(IsExecutingProperty, value);
        }

        private bool _dragging;
        private Point _dragStartPoint;
        private double _dragStartX;
        private double _dragStartY;

        public NodeView()
        {
            InitializeComponent();
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;
            MouseDoubleClick += OnMouseDoubleClick;
        }

        /// <summary>한 노드가 더블클릭됐을 때 발화. 호스트(Sample) 가 인스펙터를 편집 모드로 전환.</summary>
        public static readonly System.Windows.RoutedEvent NodeDoubleClickedEvent =
            EventManager.RegisterRoutedEvent(nameof(NodeDoubleClicked),
                RoutingStrategy.Bubble, typeof(System.Windows.RoutedEventHandler), typeof(NodeView));

        public event System.Windows.RoutedEventHandler NodeDoubleClicked
        {
            add => AddHandler(NodeDoubleClickedEvent, value);
            remove => RemoveHandler(NodeDoubleClickedEvent, value);
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not NodeViewModel nvm) return;

            var canvas = FindNodeGraphCanvas(this);
            var graph = canvas?.Graph;

            // 선택 처리 — modifier 별로 분기.
            // - Ctrl: 이 노드를 선택 토글 (기존 선택 유지). 토글 결과 선택 해제면 드래그 안 함.
            // - Shift: 기존 선택에 이 노드 추가.
            // - 그 외: 이 노드가 이미 다중 선택에 포함되어 있으면 선택 상태 유지(전체 드래그 의도),
            //         아니면 단일 선택으로 리셋.
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (graph != null)
            {
                if (ctrl)
                {
                    graph.ToggleInSelection(nvm);
                }
                else if (shift)
                {
                    graph.AddToSelection(nvm);
                }
                else
                {
                    // 이미 다중 선택의 일부면 그대로 둠 (드래그 시 묶음 이동 의도).
                    if (!graph.SelectedNodes.Contains(nvm))
                    {
                        graph.SelectedNode = nvm; // setter 가 SelectedNodes 도 단일로 리셋
                    }
                    else
                    {
                        // 묶음 중 하나를 다시 클릭 — SelectedNode 만 "활성 노드" 로 갱신, 묶음은 유지.
                        // 이를 위해 ViewModel 의 suppressSync 경로가 필요한데, 단순화: SelectedNodes 가 이미
                        // 이 노드를 포함하므로 AddToSelection 호출이 안전 (no-op 이지만 SelectedNode 만 갱신).
                        graph.AddToSelection(nvm);
                    }
                }
                if (graph.SelectedConnection != null) graph.SelectedConnection = null;
                canvas.Focus(); // Delete 키 동작 보장
            }

            // 드래그 시작 — Ctrl 토글로 선택이 해제됐으면 드래그 안 함.
            if (graph != null && !graph.SelectedNodes.Contains(nvm))
            {
                _dragging = false;
                e.Handled = true;
                return;
            }

            _dragging = true;
            _dragStartPoint = e.GetPosition(Parent as IInputElement);
            _dragStartX = nvm.X;
            _dragStartY = nvm.Y;
            CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new System.Windows.RoutedEventArgs(NodeDoubleClickedEvent, this));
        }

        private static NodeGraphCanvas FindNodeGraphCanvas(DependencyObject start)
        {
            var cur = VisualTreeHelper.GetParent(start);
            while (cur != null)
            {
                if (cur is NodeGraphCanvas c) return c;
                cur = VisualTreeHelper.GetParent(cur);
            }
            return null;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                ReleaseMouseCapture();

                // 다중 선택 이동이었으면 — 묶음 전체의 새 좌표를 Model.MoveNode 로 commit (undo 가능).
                // 단일 노드는 NodeViewModel.X/Y 변경이 OnGraphChanged 의 Moved 푸시를 트리거하지 않으므로,
                // (Moved 이벤트는 Model.MoveNode 호출에서만 발화) 항상 commit 필요.
                var canvas = FindNodeGraphCanvas(this);
                var graph = canvas?.Graph;
                if (graph != null && DataContext is NodeViewModel nvm)
                {
                    if (graph.SelectedNodes.Count > 1 && graph.SelectedNodes.Contains(nvm))
                    {
                        graph.CommitSelectionMove();
                    }
                    else
                    {
                        // 단일 노드 — Model.MoveNode 1회 호출. 안 움직였으면 0 px diff 라도 한 번 푸시.
                        graph.Model.MoveNode(nvm.Id, nvm.X, nvm.Y);
                    }
                }
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            if (DataContext is not NodeViewModel nvm) return;
            var current = e.GetPosition(Parent as IInputElement);

            // 현재 노드의 새 좌표(절대).
            var newX = _dragStartX + (current.X - _dragStartPoint.X);
            var newY = _dragStartY + (current.Y - _dragStartPoint.Y);

            // 다중 선택의 일부면 — 다른 선택 노드들도 같은 delta 만큼 따라 이동.
            var canvas = FindNodeGraphCanvas(this);
            var graph = canvas?.Graph;
            if (graph != null && graph.SelectedNodes.Count > 1 && graph.SelectedNodes.Contains(nvm))
            {
                var dx = newX - nvm.X;
                var dy = newY - nvm.Y;
                nvm.X = newX;
                nvm.Y = newY;
                graph.TranslateSelectionBy(dx, dy, except: nvm);
            }
            else
            {
                nvm.X = newX;
                nvm.Y = newY;
            }
        }

        /// <summary>
        /// 노드 내의 특정 핀의 중심점을 노드 좌상단(NodeView 자기 자신) 기준 상대 좌표로 반환.
        /// 시각 트리에서 PinView 를 찾아 TransformToAncestor 로 측정. 레이아웃 미완료면 (0,0).
        /// </summary>
        public Point GetPinCenterRelativeToNode(string pinId)
        {
            if (string.IsNullOrEmpty(pinId)) return new Point(0, 0);
            var pinView = FindPinView(this, pinId);
            if (pinView == null) return new Point(0, 0);
            try
            {
                var transform = pinView.TransformToAncestor(this);
                var topLeft = transform.Transform(new Point(0, 0));
                return new Point(topLeft.X + pinView.ActualWidth / 2.0,
                                 topLeft.Y + pinView.ActualHeight / 2.0);
            }
            catch (System.InvalidOperationException)
            {
                // 시각 트리 분리 상태 — 레이아웃 미완료
                return new Point(0, 0);
            }
        }

        private static PinView FindPinView(DependencyObject root, string pinId)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is PinView pv && pv.DataContext is PinViewModel pvm && pvm.Id == pinId)
                {
                    return pv;
                }
                var nested = FindPinView(child, pinId);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
