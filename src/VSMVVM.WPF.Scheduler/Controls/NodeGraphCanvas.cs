using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.WPF.Controls;
using VSMVVM.WPF.Scheduler.ViewModels;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>
    /// Blueprint 스타일 노드 그래프 캔버스.
    /// LayeredCanvas와 sibling 관계 — 이미지/마스크 도메인 특화 기능은 상속하지 않고 IZoomPanViewport만 구현하여
    /// 기존 MiniMapControl과 호환 유지. 줌/팬 수학은 LayeredCanvas에서 추출/복사한 형태.
    ///
    /// 내부 z-레이어 (자식 순서):
    ///   1) _gridLayer       (그리드 배경 — DrawingBrush로 점 패턴)
    ///   2) _connectionLayer (연결선)
    ///   3) _nodeLayer       (노드 컨테이너)
    ///   4) _previewLayer    (드래그 중 연결 프리뷰)
    /// </summary>
    public class NodeGraphCanvas : Canvas, IZoomPanViewport
    {
        public static readonly DependencyProperty GraphProperty =
            DependencyProperty.Register(nameof(Graph), typeof(NodeGraphViewModel), typeof(NodeGraphCanvas),
                new PropertyMetadata(null, OnGraphChanged));

        public static readonly DependencyProperty MinZoomProperty =
            DependencyProperty.Register(nameof(MinZoom), typeof(double), typeof(NodeGraphCanvas),
                new PropertyMetadata(0.25));

        public static readonly DependencyProperty MaxZoomProperty =
            DependencyProperty.Register(nameof(MaxZoom), typeof(double), typeof(NodeGraphCanvas),
                new PropertyMetadata(4.0));

        private readonly Canvas _gridLayer = new() { Background = Brushes.Transparent };
        private readonly Canvas _connectionLayer = new() { Background = null };
        private readonly Canvas _nodeLayer = new() { Background = null };
        private readonly Canvas _previewLayer = new() { Background = null, IsHitTestVisible = false };

        private readonly TranslateTransform _translate = new();
        private readonly ScaleTransform _scale = new();
        private readonly TransformGroup _viewTransform = new();

        private readonly Dictionary<Guid, FrameworkElement> _nodeElements = new();
        private readonly Dictionary<Guid, ConnectionView> _connectionElements = new();

        // 드래그 상태
        private bool _isPanning;
        private Point _panStart;
        private double _panStartOffsetX;
        private double _panStartOffsetY;
        private bool _panMoved; // 우클릭 → 팬 vs 컨텍스트 메뉴 구분용 (드래그 임계값 초과 여부)

        // 좌클릭 박스 선택(marquee) 상태 — 빈 캔버스 좌클릭 드래그.
        private bool _isMarquee;
        private Point _marqueeStartScreen;   // 시작 화면 좌표 (canvas 좌상단 기준)
        private Point _marqueeStartCanvas;   // 시작 캔버스 좌표 (zoom/pan 반영)
        private ModifierKeys _marqueeStartModifiers; // 시작 시점 modifier — 박스가 그려질 때까지 보존.
        private Rectangle _marqueeRect;      // 화면 공간에 그려지는 점선 박스 (캔버스 자식, view transform 미적용)
        private HashSet<NodeViewModel> _marqueeBaseSelection; // Shift/Ctrl 드래그 시작 시점의 기존 선택 집합 (모디파이어 박스의 base 로 사용)

        public NodeGraphViewModel Graph
        {
            get => (NodeGraphViewModel)GetValue(GraphProperty);
            set => SetValue(GraphProperty, value);
        }

        public double MinZoom
        {
            get => (double)GetValue(MinZoomProperty);
            set => SetValue(MinZoomProperty, value);
        }

        public double MaxZoom
        {
            get => (double)GetValue(MaxZoomProperty);
            set => SetValue(MaxZoomProperty, value);
        }

        public NodeGraphCanvas()
        {
            Background = Brushes.Transparent;
            ClipToBounds = true;
            Focusable = true;
            // 팔레트 드래그-앤-드롭 수용 — Drop 시 라우티드 이벤트로 호스트에 위임.
            AllowDrop = true;
            DragOver += OnPaletteDragOver;
            Drop += OnPaletteDrop;

            _viewTransform.Children.Add(_scale);
            _viewTransform.Children.Add(_translate);

            // 자식 레이어 등록 — Canvas는 IsItemsHost 아니므로 직접 Children.Add
            Children.Add(_gridLayer);
            Children.Add(_connectionLayer);
            Children.Add(_nodeLayer);
            Children.Add(_previewLayer);

            // 각 레이어에 view transform 적용
            _gridLayer.RenderTransform = _viewTransform;
            _connectionLayer.RenderTransform = _viewTransform;
            _nodeLayer.RenderTransform = _viewTransform;
            _previewLayer.RenderTransform = _viewTransform;

            MouseRightButtonDown += OnRightButtonDown;
            MouseRightButtonUp += OnRightButtonUp;
            MouseLeftButtonDown += OnLeftButtonDown;
            MouseLeftButtonUp += OnLeftButtonUp;
            MouseMove += OnMouseMove;
            MouseWheel += OnMouseWheel;
            SizeChanged += OnSizeChanged_Internal;
            KeyDown += OnKeyDown_Internal;

            // 핀 드래그 라우티드 이벤트 구독 (자식 PinView에서 bubble로 올라옴)
            AddHandler(PinConnectionRoutedEvents.ConnectionDragStartedEvent,
                new PinConnectionRoutedEventHandler(OnPinConnectionDragStarted));
            AddHandler(PinConnectionRoutedEvents.ConnectionDragCompletedEvent,
                new PinConnectionRoutedEventHandler(OnPinConnectionDragCompleted));
        }

        #region Palette drag-and-drop

        /// <summary>드래그 페이로드에 NodePaletteEntry 가 있을 때만 Copy 효과로 수용.</summary>
        private void OnPaletteDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(NodePaletteDragFormats.PaletteEntry))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        /// <summary>
        /// 드롭된 NodePaletteEntry 를 캔버스 좌표로 변환 후 라우티드 이벤트로 호스트에 위임.
        /// 호스트(WorkspaceView) 가 PaletteEntryDroppedEvent 를 캐치해 워크스페이스에 노드 추가.
        /// </summary>
        private void OnPaletteDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(NodePaletteDragFormats.PaletteEntry)) return;
            var entry = e.Data.GetData(NodePaletteDragFormats.PaletteEntry) as Services.NodePaletteEntry;
            if (entry == null) return;

            var screen = e.GetPosition(this);
            var canvasPoint = ScreenToCanvas(screen);

            RaiseEvent(new NodePaletteDropEventArgs(
                NodePaletteDropRoutedEvents.PaletteEntryDroppedEvent, this, entry, canvasPoint));
            e.Handled = true;
        }

        #endregion

        #region Pin connection (drag OR click-to-click)

        // 출발 핀 — 드래그 모드와 클릭/클릭 모드가 공유. null 이면 비활성.
        private PinViewModel _dragSourcePin;
        private ConnectionView _previewConnection;
        private Point _dragStartCanvasPoint;
        // 핀 MouseDown 직후 보류 상태 — 첫 MouseMove 가 임계값 넘으면 드래그, MouseUp 이 먼저 오면 클릭.
        private bool _pinPressPending;
        private Point _pinPressScreenStart;
        // 클릭/클릭 연결 모드 — 첫 핀 클릭으로 활성화. 두 번째 핀 클릭 또는 빈 영역 클릭/Esc 로 종료.
        private bool _isClickConnectMode;
        // 드래그 모드(마우스 캡처 + preview) 활성 여부. _dragSourcePin 만으로는 클릭 모드와 구분 불가하므로 별도 플래그.
        private bool _isDragConnectActive;
        private const double PinDragThreshold = 4.0;

        private void OnPinConnectionDragStarted(object sender, PinConnectionRoutedEventArgs e)
        {
            // 출력 핀에서 시작 = 정방향, 입력 핀에서 시작 = 역방향 — 양쪽 모두 허용.
            if (e.Pin == null) return;

            // 클릭/클릭 모드 활성 중 — 이번 핀 다운은 "두 번째 핀 클릭" 후보. 임계값 미만이면 MouseUp 에서 연결 시도.
            if (_isClickConnectMode)
            {
                _pinPressPending = true;
                _pinPressScreenStart = Mouse.GetPosition(this);
                e.Handled = true;
                return;
            }

            // 신규 클릭 — 보류 상태로 진입. 드래그/클릭 분기는 MouseMove/MouseUp 가 결정.
            _dragSourcePin = e.Pin;
            _pinPressPending = true;
            _pinPressScreenStart = Mouse.GetPosition(this);

            // 시작 핀의 캔버스 좌표 측정 (preview 시작점 — 드래그가 시작될 때 사용. 클릭 모드 진입 시도 동일.)
            if (e.OriginalSource is FrameworkElement pinEl)
            {
                var transform = pinEl.TransformToVisual(this);
                var center = transform.Transform(new Point(pinEl.ActualWidth / 2, pinEl.ActualHeight / 2));
                _dragStartCanvasPoint = ScreenToCanvas(center);
            }
            e.Handled = true;
        }

        /// <summary>
        /// 드래그 모드 활성화 — 임계값 넘은 첫 MouseMove 가 호출. preview 생성 + 캡처 + 호환 핀 강조.
        /// </summary>
        private void BeginDragConnect()
        {
            if (_dragSourcePin == null) return;
            _isDragConnectActive = true;
            _dragSourcePin.IsConnectionSource = true;
            HighlightCompatibleTargets(_dragSourcePin);

            _previewConnection = new ConnectionView
            {
                StrokeBrush = _dragSourcePin.IsExec
                    ? (Brush)(TryFindResource("TextPrimary") ?? Brushes.White)
                    : Brushes.Gray,
                StrokeThickness = 2.0,
                IsHitTestVisible = false,
                Start = _dragStartCanvasPoint,
                End = _dragStartCanvasPoint,
            };
            _previewLayer.Children.Add(_previewConnection);
            CaptureMouse();
        }

        /// <summary>
        /// 클릭/클릭 모드 활성화 — MouseUp 이 임계값 미만 이동에서 호출. preview 생성 + 호환 핀 강조.
        /// 캔버스 캡처는 하지 않음 — 다른 핀에 호버/클릭이 가야 하므로.
        /// </summary>
        private void BeginClickConnect()
        {
            if (_dragSourcePin == null) return;
            _isClickConnectMode = true;
            _dragSourcePin.IsConnectionSource = true;
            HighlightCompatibleTargets(_dragSourcePin);

            _previewConnection = new ConnectionView
            {
                StrokeBrush = _dragSourcePin.IsExec
                    ? (Brush)(TryFindResource("TextPrimary") ?? Brushes.White)
                    : Brushes.Gray,
                StrokeThickness = 2.0,
                IsHitTestVisible = false,
                Start = _dragStartCanvasPoint,
                End = _dragStartCanvasPoint,
            };
            _previewLayer.Children.Add(_previewConnection);
        }

        /// <summary>
        /// 출발 핀과 호환되는 모든 입력/출력 핀에 IsCompatibleTarget=true.
        /// PinCompatibility.CanConnect 로 판정 — 출발이 Output 이면 다른 노드의 Input 들, 입력이면 다른 노드의 Output 들.
        /// </summary>
        private void HighlightCompatibleTargets(PinViewModel source)
        {
            if (Graph == null || source == null) return;
            foreach (var n in Graph.Nodes)
            {
                var candidates = source.IsOutput ? (System.Collections.Generic.IEnumerable<PinViewModel>)n.InputPins : n.OutputPins;
                foreach (var p in candidates)
                {
                    if (p == source) continue;
                    var src = source.IsOutput ? source : p;
                    var dst = source.IsOutput ? p : source;
                    if (PinCompatibility.CanConnect(src.Model, dst.Model, out _))
                    {
                        p.IsCompatibleTarget = true;
                    }
                }
            }
        }

        private void ClearCompatibleHighlights()
        {
            if (Graph == null) return;
            foreach (var n in Graph.Nodes)
            {
                foreach (var p in n.InputPins) { p.IsCompatibleTarget = false; }
                foreach (var p in n.OutputPins) { p.IsCompatibleTarget = false; }
            }
        }

        /// <summary>
        /// PinView 의 MouseUp 라우티드 이벤트. 두 가지 시나리오:
        /// 1) _dragSourcePin 보류 중 (드래그 임계값 미만 → 클릭) — 같은 핀에서 release 면 클릭/클릭 모드 시작.
        ///    클릭/클릭 모드 활성 중에 다른 핀에서 release 면 연결 완료.
        /// 2) 드래그 모드 활성 중 — 캔버스의 OnLeftButtonUp 이 위치 hit-test 로 처리하므로 여기는 미도달이 정상.
        ///    (캡처 중엔 PinView 의 라우티드 이벤트가 발화 안 함.)
        /// </summary>
        private void OnPinConnectionDragCompleted(object sender, PinConnectionRoutedEventArgs e)
        {
            // 클릭/클릭 모드 — 두 번째 핀에서 release.
            if (_isClickConnectMode && e.Pin != null && e.Pin != _dragSourcePin)
            {
                TryCompleteConnectionAt(e.Pin);
                e.Handled = true;
                return;
            }

            // 보류 중 + 임계값 미만 → 클릭 모드 시작 (또는 같은 핀이 두 번째 핀이면 취소).
            if (_pinPressPending && !_isDragConnectActive)
            {
                _pinPressPending = false;
                var releaseScreen = Mouse.GetPosition(this);
                var dx = releaseScreen.X - _pinPressScreenStart.X;
                var dy = releaseScreen.Y - _pinPressScreenStart.Y;
                bool wasClick = (dx * dx + dy * dy) <= PinDragThreshold * PinDragThreshold;
                if (wasClick)
                {
                    if (_isClickConnectMode && e.Pin == _dragSourcePin)
                    {
                        // 같은 핀 다시 클릭 = 취소
                        CancelPreviewConnection();
                    }
                    else if (!_isClickConnectMode)
                    {
                        BeginClickConnect();
                    }
                }
                else
                {
                    // 임계값 넘었지만 MouseMove 가 BeginDragConnect 를 못 부른 경우(빠른 미세 진동) — 그냥 취소.
                    CancelPreviewConnection();
                }
                e.Handled = true;
                return;
            }
        }

        /// <summary>
        /// 캔버스 좌표(혹은 PinView hit-test 결과) 의 도착 핀과 연결 생성 시도.
        /// 양방향 허용: 출력→입력 또는 입력→출력 모두 OK. 같은 방향끼리는 거부 (Graph.Connect 가 throw).
        /// </summary>
        private void TryCompleteConnectionAt(PinViewModel target)
        {
            try
            {
                if (target == null || target == _dragSourcePin) return;
                if (Graph?.ConnectCommand == null) return;
                // 방향 정렬: source = Output, target = Input
                var src = _dragSourcePin.IsOutput ? _dragSourcePin : target;
                var dst = _dragSourcePin.IsOutput ? target : _dragSourcePin;
                if (!src.IsOutput || !dst.IsInput) return; // 같은 방향끼리는 무시
                Graph.ConnectCommand.Execute((src, dst));
            }
            finally
            {
                CancelPreviewConnection();
            }
        }

        private void CancelPreviewConnection()
        {
            if (_previewConnection != null)
            {
                _previewLayer.Children.Remove(_previewConnection);
                _previewConnection = null;
            }
            if (_dragSourcePin != null) _dragSourcePin.IsConnectionSource = false;
            ClearCompatibleHighlights();
            _dragSourcePin = null;
            _pinPressPending = false;
            _isClickConnectMode = false;
            _isDragConnectActive = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
        }

        /// <summary>마우스 위치의 PinView 를 시각 트리에서 찾아 PinViewModel 반환. 없으면 null.</summary>
        private PinViewModel HitTestPin(Point screenPos)
        {
            var hit = InputHitTest(screenPos) as DependencyObject;
            while (hit != null)
            {
                if (hit is PinView pv && pv.DataContext is PinViewModel pvm) return pvm;
                hit = VisualTreeHelper.GetParent(hit);
            }
            return null;
        }

        #endregion

        #region Graph binding

        private static void OnGraphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (NodeGraphCanvas)d;
            if (e.OldValue is NodeGraphViewModel oldVm)
            {
                ((INotifyCollectionChanged)oldVm.Nodes).CollectionChanged -= canvas.OnNodesChanged;
                ((INotifyCollectionChanged)oldVm.Connections).CollectionChanged -= canvas.OnConnectionsChanged;
                oldVm.PropertyChanged -= canvas.OnGraphPropertyChanged;
            }
            canvas.ClearAllItems();
            if (e.NewValue is NodeGraphViewModel newVm)
            {
                ((INotifyCollectionChanged)newVm.Nodes).CollectionChanged += canvas.OnNodesChanged;
                ((INotifyCollectionChanged)newVm.Connections).CollectionChanged += canvas.OnConnectionsChanged;
                newVm.PropertyChanged += canvas.OnGraphPropertyChanged;
                foreach (var n in newVm.Nodes) canvas.AddNodeElement(n);
                foreach (var c in newVm.Connections) canvas.AddConnectionElement(c);
            }
            canvas.RecomputeContentBounds();
        }

        /// <summary>Graph.SelectedConnection / LayoutOrientation 변경 시 각 ConnectionView 동기화.</summary>
        private void OnGraphPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NodeGraphViewModel.SelectedConnection))
            {
                var selected = Graph?.SelectedConnection;
                foreach (var kv in _connectionElements)
                {
                    kv.Value.IsSelected = kv.Value.Tag is ConnectionViewModel cvm && cvm == selected;
                }
            }
            else if (e.PropertyName == nameof(NodeGraphViewModel.LayoutOrientation))
            {
                var orientation = Graph?.LayoutOrientation ?? GraphLayoutOrientation.Horizontal;
                foreach (var kv in _connectionElements)
                {
                    kv.Value.Orientation = orientation;
                }
            }
        }

        private void OnNodesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (NodeViewModel n in e.NewItems) AddNodeElement(n);
            if (e.OldItems != null)
                foreach (NodeViewModel n in e.OldItems) RemoveNodeElement(n);
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                _nodeLayer.Children.Clear();
                _nodeElements.Clear();
            }
        }

        private void OnConnectionsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ConnectionViewModel c in e.NewItems) AddConnectionElement(c);
            if (e.OldItems != null)
                foreach (ConnectionViewModel c in e.OldItems) RemoveConnectionElement(c);
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                _connectionLayer.Children.Clear();
                _connectionElements.Clear();
            }
        }

        protected virtual void AddNodeElement(NodeViewModel nvm)
        {
            // 직접 NodeView 인스턴스화 — implicit DataTemplate 은 호스트가 Generic.xaml 을 명시 머지하지 않으면
            // 동작하지 않으므로 (Scheduler 어셈블리에 [ThemeInfo] 없음), DataTemplate 의존성을 피하고
            // 컨트롤 스스로 시각화를 보장.
            var view = new NodeView { DataContext = nvm };
            Canvas.SetLeft(view, nvm.X);
            Canvas.SetTop(view, nvm.Y);
            // 노드 위치 추적 — X/Y 변경 시 캔버스 위치 + ContentBounds 즉시 갱신 (미니맵 동기화).
            // 디버그 정지 위치 추적 — IsCurrentBreakpoint=true 가 된 노드를 viewport 중심으로 자동 panning.
            nvm.PropertyChanged += (_, ev) =>
            {
                if (ev.PropertyName == nameof(NodeViewModel.X))
                {
                    Canvas.SetLeft(view, nvm.X);
                    RecomputeContentBounds();
                }
                else if (ev.PropertyName == nameof(NodeViewModel.Y))
                {
                    Canvas.SetTop(view, nvm.Y);
                    RecomputeContentBounds();
                }
                else if (ev.PropertyName == nameof(NodeViewModel.IsCurrentBreakpoint) && nvm.IsCurrentBreakpoint)
                {
                    CenterOnNode(nvm);
                }
            };
            // 레이아웃 완료 시 이 노드를 양 끝으로 갖는 모든 connection 의 핀 오프셋 재측정.
            view.LayoutUpdated += (_, _) => RefreshPinOffsetsForNode(nvm);
            _nodeElements[nvm.Id] = view;
            _nodeLayer.Children.Add(view);
            RecomputeContentBounds();
        }

        /// <summary>
        /// 노드 ViewModel 의 핀 위치(노드 좌상단 기준) 가 NodeView 의 시각 트리에서 측정 가능해진 뒤,
        /// 이 노드를 source 또는 target 으로 갖는 모든 ConnectionViewModel 의 SourcePinOffset/TargetPinOffset 을 갱신.
        /// 결과적으로 ConnectionViewModel.Start/End 가 정확한 핀 중심으로 다시 계산됨.
        /// </summary>
        private void RefreshPinOffsetsForNode(NodeViewModel nvm)
        {
            if (Graph == null) return;
            if (!_nodeElements.TryGetValue(nvm.Id, out var elem) || elem is not NodeView nodeView) return;
            foreach (var cvm in Graph.Connections)
            {
                if (cvm.Source == nvm)
                {
                    cvm.SourcePinOffset = nodeView.GetPinCenterRelativeToNode(cvm.SourcePin.Id);
                }
                if (cvm.Target == nvm)
                {
                    cvm.TargetPinOffset = nodeView.GetPinCenterRelativeToNode(cvm.TargetPin.Id);
                }
            }
        }

        protected virtual void RemoveNodeElement(NodeViewModel nvm)
        {
            if (_nodeElements.TryGetValue(nvm.Id, out var elem))
            {
                _nodeLayer.Children.Remove(elem);
                _nodeElements.Remove(nvm.Id);
            }
            RecomputeContentBounds();
        }

        // 노드 본체 추정 폭/높이 — ContentBounds 계산에만 사용 (미니맵 사각형 크기 추정과 일관성 유지).
        private const double NodeApproxWidthForBounds = 160;
        private const double NodeApproxHeightForBounds = 80;

        /// <summary>
        /// 현재 노드들의 X/Y union 을 IZoomPanViewport.ContentWidth/Height 에 반영.
        /// MiniMapControl 이 0 보다 큰 ContentSize 를 봐야 그리기/클릭 처리가 활성화된다.
        /// </summary>
        private void RecomputeContentBounds()
        {
            if (Graph == null || Graph.Nodes.Count == 0)
            {
                ContentWidth = 0;
                ContentHeight = 0;
                return;
            }
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var n in Graph.Nodes)
            {
                if (n.X < minX) minX = n.X;
                if (n.Y < minY) minY = n.Y;
                if (n.X + NodeApproxWidthForBounds > maxX) maxX = n.X + NodeApproxWidthForBounds;
                if (n.Y + NodeApproxHeightForBounds > maxY) maxY = n.Y + NodeApproxHeightForBounds;
            }
            ContentWidth = maxX;  // 미니맵은 (0,0) 부터 ContentSize 로 가정하므로 원점부터 max 좌표까지로 노출.
            ContentHeight = maxY;
            RaiseViewportChanged();
        }

        protected virtual void AddConnectionElement(ConnectionViewModel cvm)
        {
            var view = new ConnectionView();
            view.SetBinding(ConnectionView.StartProperty, new System.Windows.Data.Binding(nameof(ConnectionViewModel.Start)) { Source = cvm });
            view.SetBinding(ConnectionView.EndProperty, new System.Windows.Data.Binding(nameof(ConnectionViewModel.End)) { Source = cvm });
            // Phase 11: N:M 시각화 — 형제 연결이 여럿일 때 곱률 오프셋으로 부채꼴 분리
            view.SetBinding(ConnectionView.CurvatureOffsetProperty,
                new System.Windows.Data.Binding(nameof(ConnectionViewModel.CurvatureOffset)) { Source = cvm });
            view.PinKind = cvm.SourcePin.Kind;
            // 현재 그래프의 정렬 방향을 따라 베지어 컨트롤 포인트 방향 결정.
            view.Orientation = Graph?.LayoutOrientation ?? GraphLayoutOrientation.Horizontal;
            // Data 연결은 핀 타입 색 적용 (Phase 5는 단순화 — exec=white, data=gray, 추후 PinTypeBrushConverter 연동)
            view.StrokeBrush = cvm.SourcePin.Kind == PinKind.Exec ? Brushes.White : Brushes.Gray;
            view.Tag = cvm; // 클릭 시 선택할 ViewModel 식별용
            view.MouseLeftButtonDown += OnConnectionClicked;
            _connectionElements[cvm.Id] = view;
            _connectionLayer.Children.Add(view);
        }

        private void OnConnectionClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is ConnectionView cv && cv.Tag is ConnectionViewModel cvm && Graph != null)
            {
                Graph.SelectedConnection = cvm;
                Graph.SelectedNode = null; // 둘 중 하나만 선택
                Focus(); // Delete 키가 동작하도록
                e.Handled = true;
            }
        }

        protected virtual void RemoveConnectionElement(ConnectionViewModel cvm)
        {
            if (_connectionElements.TryGetValue(cvm.Id, out var view))
            {
                _connectionLayer.Children.Remove(view);
                _connectionElements.Remove(cvm.Id);
            }
        }

        private void ClearAllItems()
        {
            _nodeLayer.Children.Clear();
            _connectionLayer.Children.Clear();
            _nodeElements.Clear();
            _connectionElements.Clear();
        }

        #endregion

        #region Pan / Zoom

        private const double PanThreshold = 5.0;

        private void OnRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isPanning = true;
            _panMoved = false;
            _panStart = Mouse.GetPosition(this);
            _panStartOffsetX = _translate.X;
            _panStartOffsetY = _translate.Y;
            CaptureMouse();
            e.Handled = true;
        }

        /// <summary>
        /// 빈 캔버스 좌클릭 드래그 = 박스 선택(marquee). 패닝은 우클릭 드래그로 이관.
        /// NodeView 가 자기 클릭은 e.Handled=true 로 잡으므로 빈 영역에서만 도달.
        /// Modifier 없으면 단순 클릭이 기존 선택을 해제, Shift/Ctrl 누르면 기존 선택을 base 로 두고 박스 영역만 토글/추가.
        /// 핀 클릭/클릭 모드 활성 중이면 — 빈 영역 클릭으로 모드 취소.
        /// </summary>
        private void OnLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 클릭/클릭 연결 모드 활성 중 — 빈 영역 좌클릭 = 모드 취소. marquee 시작 안 함.
            if (_isClickConnectMode)
            {
                CancelPreviewConnection();
                e.Handled = true;
                return;
            }

            // 드래그 모드/보류 진행 중이면 박스 선택 안 함.
            if (_dragSourcePin != null) return;

            Focus(); // Delete/단축키 동작 보장.

            _marqueeStartScreen = Mouse.GetPosition(this);
            _marqueeStartCanvas = ScreenToCanvas(_marqueeStartScreen);
            _marqueeStartModifiers = Keyboard.Modifiers;
            _isMarquee = true;
            _panMoved = false; // marquee 가 임계값 넘기 전엔 단순 클릭 처리.

            // Shift/Ctrl 없으면 즉시 선택 해제 (단순 클릭/박스 드래그 모두 새 선택으로 시작).
            // Shift/Ctrl 누르면 기존 선택을 base 로 보존.
            bool keepExisting = (_marqueeStartModifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0;
            if (Graph != null)
            {
                if (!keepExisting)
                {
                    Graph.ClearSelection();
                }
                if (Graph.SelectedConnection != null) Graph.SelectedConnection = null;
                _marqueeBaseSelection = keepExisting
                    ? new HashSet<NodeViewModel>(Graph.SelectedNodes)
                    : new HashSet<NodeViewModel>();
            }

            CaptureMouse();
            e.Handled = true;
        }

        /// <summary>화면 좌표의 임의 두 점이 만드는 사각형으로 marquee 박스 시각화 (점선 사각형). 캔버스 자식이지만 view transform 미적용.</summary>
        private void UpdateMarqueeRect(Point a, Point b)
        {
            var x = Math.Min(a.X, b.X);
            var y = Math.Min(a.Y, b.Y);
            var w = Math.Abs(a.X - b.X);
            var h = Math.Abs(a.Y - b.Y);
            if (_marqueeRect == null)
            {
                _marqueeRect = new Rectangle
                {
                    Stroke = (Brush)(TryFindResource("AccentSecondary") ?? Brushes.DodgerBlue),
                    StrokeThickness = 1.0,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    Fill = new SolidColorBrush(Color.FromArgb(40, 100, 149, 237)),
                    IsHitTestVisible = false,
                };
                Children.Add(_marqueeRect); // 최상위 — view transform 영향 받지 않음.
            }
            Canvas.SetLeft(_marqueeRect, x);
            Canvas.SetTop(_marqueeRect, y);
            _marqueeRect.Width = w;
            _marqueeRect.Height = h;
        }

        private void EndMarquee(Point endScreen)
        {
            if (_marqueeRect != null)
            {
                Children.Remove(_marqueeRect);
                _marqueeRect = null;
            }
            if (Graph == null) { _isMarquee = false; return; }

            // 시작/끝 화면 좌표 → 캔버스 좌표로 변환. 노드와 교차 검사.
            var endCanvas = ScreenToCanvas(endScreen);
            var rx = Math.Min(_marqueeStartCanvas.X, endCanvas.X);
            var ry = Math.Min(_marqueeStartCanvas.Y, endCanvas.Y);
            var rw = Math.Abs(_marqueeStartCanvas.X - endCanvas.X);
            var rh = Math.Abs(_marqueeStartCanvas.Y - endCanvas.Y);
            var marqueeCanvas = new Rect(rx, ry, rw, rh);

            // 노드 박스 추정 — 캔버스 ContentBounds 계산과 동일 가정 폭/높이 사용.
            var hit = new List<NodeViewModel>();
            foreach (var n in Graph.Nodes)
            {
                var nodeRect = new Rect(n.X, n.Y, NodeApproxWidthForBounds, NodeApproxHeightForBounds);
                if (marqueeCanvas.IntersectsWith(nodeRect)) hit.Add(n);
            }

            bool ctrl = (_marqueeStartModifiers & ModifierKeys.Control) != 0;

            if (ctrl)
            {
                // Ctrl+박스 — base 와 hit 의 대칭차(XOR): base 에만 있으면 유지, 둘 다 있으면 제거, hit 에만 있으면 추가.
                Graph.ClearSelection();
                var xor = new HashSet<NodeViewModel>(_marqueeBaseSelection);
                xor.SymmetricExceptWith(hit);
                Graph.AddRangeToSelection(xor);
            }
            else
            {
                // Modifier 없거나 Shift — base 와 hit 의 합집합 (Shift 모드)/hit 자체 (no modifier, ClearSelection 후 hit 만).
                // 둘 다 같은 코드 경로 — base 가 이미 비어있으면 합집합이 hit 자체와 같음.
                var union = new HashSet<NodeViewModel>(_marqueeBaseSelection);
                foreach (var h in hit) union.Add(h);
                Graph.ClearSelection();
                Graph.AddRangeToSelection(union);
            }

            _isMarquee = false;
            _marqueeBaseSelection = null;
        }

        /// <summary>
        /// 키보드 단축키:
        ///   • Delete/Backspace — 선택 노드(다중 가능) 또는 연결 삭제. 노드 우선.
        ///   • Ctrl+C — 선택 복사 (앱 내부 클립보드).
        ///   • Ctrl+V — 붙여넣기.
        ///   • Ctrl+A — 모든 노드 선택.
        ///   • Ctrl+L — Topological-columns 자동 정렬.
        /// </summary>
        private void OnKeyDown_Internal(object sender, KeyEventArgs e)
        {
            // Esc — 어떤 모드든 취소 우선. 핀 클릭/클릭 모드, 핀 드래그 모드 모두.
            if (e.Key == Key.Escape)
            {
                if (_isClickConnectMode || _isDragConnectActive || _dragSourcePin != null)
                {
                    CancelPreviewConnection();
                    e.Handled = true;
                    return;
                }
            }

            if (Graph == null) return;

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                if ((Graph.SelectedNodes.Count > 0 || Graph.SelectedNode != null)
                    && Graph.RemoveSelectedCommand?.CanExecute(null) == true)
                {
                    Graph.RemoveSelectedCommand.Execute(null);
                    e.Handled = true;
                }
                else if (Graph.SelectedConnection != null
                         && Graph.DisconnectCommand?.CanExecute(Graph.SelectedConnection) == true)
                {
                    Graph.DisconnectCommand.Execute(Graph.SelectedConnection);
                    Graph.SelectedConnection = null;
                    e.Handled = true;
                }
                return;
            }

            if (!ctrl) return;

            switch (e.Key)
            {
                case Key.C:
                    if (Graph.CopyCommand?.CanExecute(null) == true)
                    {
                        Graph.CopyCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.V:
                    if (Graph.PasteCommand?.CanExecute(null) == true)
                    {
                        Graph.PasteCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.A:
                    Graph.SelectAllCommand?.Execute(null);
                    e.Handled = true;
                    break;
                case Key.L:
                    Graph.AutoLayoutCommand?.Execute(null);
                    e.Handled = true;
                    break;
            }
        }

        private void OnLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 드래그 모드 활성 중이었다면 도착 위치의 핀에 연결 시도. (캡처 중 — PinView 의 라우티드 이벤트는 미발화.)
            if (_isDragConnectActive)
            {
                var screen = Mouse.GetPosition(this);
                var hitPin = HitTestPin(screen);
                TryCompleteConnectionAt(hitPin);
                e.Handled = true;
                return;
            }
            if (_isMarquee)
            {
                var endScreen = Mouse.GetPosition(this);
                if (_panMoved)
                {
                    EndMarquee(endScreen);
                }
                else
                {
                    // 단순 클릭 — 박스 시각화도 안 그려졌고 선택도 이미 ClearSelection 처리됨(modifier 없는 경우).
                    if (_marqueeRect != null) { Children.Remove(_marqueeRect); _marqueeRect = null; }
                    _isMarquee = false;
                    _marqueeBaseSelection = null;
                }
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void OnRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ReleaseMouseCapture();
                // 거의 안 움직였으면 컨텍스트 메뉴로 처리.
                if (!_panMoved)
                {
                    var canvasPoint = ScreenToCanvas(Mouse.GetPosition(this));
                    ContextRequested?.Invoke(this, new NodeGraphContextRequestedEventArgs(canvasPoint));
                }
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            // 핀 다운 보류 중 — 임계값 넘으면 드래그 모드 시작.
            if (_pinPressPending && !_isDragConnectActive && !_isClickConnectMode && _dragSourcePin != null)
            {
                var cur = Mouse.GetPosition(this);
                var ddx = cur.X - _pinPressScreenStart.X;
                var ddy = cur.Y - _pinPressScreenStart.Y;
                if ((ddx * ddx + ddy * ddy) > PinDragThreshold * PinDragThreshold)
                {
                    _pinPressPending = false;
                    BeginDragConnect();
                }
            }

            // 핀 드래그 모드 또는 클릭/클릭 모드 활성 — 미리보기 끝점을 마우스 위치로 추적.
            if ((_isDragConnectActive || _isClickConnectMode) && _previewConnection != null)
            {
                var screen = Mouse.GetPosition(this);
                var canvasPoint = ScreenToCanvas(screen);
                _previewConnection.Start = _dragStartCanvasPoint;
                _previewConnection.End = canvasPoint;
                return;
            }
            if (_isMarquee)
            {
                var current = Mouse.GetPosition(this);
                var dx = current.X - _marqueeStartScreen.X;
                var dy = current.Y - _marqueeStartScreen.Y;
                if (!_panMoved && (dx * dx + dy * dy) > PanThreshold * PanThreshold)
                {
                    _panMoved = true;
                }
                if (_panMoved)
                {
                    UpdateMarqueeRect(_marqueeStartScreen, current);
                }
                return;
            }
            if (_isPanning)
            {
                var current = Mouse.GetPosition(this);
                var dx = current.X - _panStart.X;
                var dy = current.Y - _panStart.Y;
                if (!_panMoved && (dx * dx + dy * dy) > PanThreshold * PanThreshold)
                {
                    _panMoved = true;
                }
                if (_panMoved)
                {
                    _translate.X = _panStartOffsetX + dx;
                    _translate.Y = _panStartOffsetY + dy;
                    RaiseViewportChanged();
                }
            }
        }

        /// <summary>빈 영역에서 우클릭(이동 없이) 시 발화. 컨텍스트 메뉴 표시용.</summary>
        public event EventHandler<NodeGraphContextRequestedEventArgs> ContextRequested;

        /// <summary>
        /// 포토샵 호환 휠 매핑:
        ///   • 휠 단독          → 세로 패닝 (Photoshop default)
        ///   • Ctrl + 휠       → 가로 패닝
        ///   • Shift + 휠      → 큰 단위 세로 패닝 (×3)
        ///   • Alt + 휠        → 줌 (커서 위치 pivot)
        /// </summary>
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

            if (alt)
            {
                var pivot = Mouse.GetPosition(this);
                var factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
                var newZoom = Clamp(_scale.ScaleX * factor, MinZoom, MaxZoom);
                ZoomAroundPivot(pivot, newZoom);
                e.Handled = true;
                return;
            }

            // 패닝 — 한 노치(Delta=120) 기준 픽셀. Shift 누르면 ×3.
            const double basePixels = 60.0;
            var step = (e.Delta / 120.0) * basePixels;
            if (shift) step *= 3.0;

            if (ctrl)
            {
                _translate.X += step;  // 가로 패닝
            }
            else
            {
                _translate.Y += step;  // 세로 패닝
            }
            RaiseViewportChanged();
            e.Handled = true;
        }

        private void ZoomAroundPivot(Point pivotScreen, double newZoom)
        {
            // 현재 캔버스 좌표 계산 후, 새 줌으로 적용하고 pivot이 같은 화면 위치를 유지하도록 offset 보정.
            var oldZoom = _scale.ScaleX;
            if (oldZoom <= 0) oldZoom = 1.0;

            // pivot의 캔버스 좌표
            var canvasX = (pivotScreen.X - _translate.X) / oldZoom;
            var canvasY = (pivotScreen.Y - _translate.Y) / oldZoom;

            _scale.ScaleX = newZoom;
            _scale.ScaleY = newZoom;

            _translate.X = pivotScreen.X - canvasX * newZoom;
            _translate.Y = pivotScreen.Y - canvasY * newZoom;
            RaiseViewportChanged();
        }

        private void OnSizeChanged_Internal(object sender, SizeChangedEventArgs e)
        {
            RaiseViewportChanged();
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        #endregion

        #region IZoomPanViewport

        public double ZoomLevel => _scale.ScaleX <= 0 ? 1.0 : _scale.ScaleX;
        public double ContentWidth { get; private set; }
        public double ContentHeight { get; private set; }
        public double ViewportWidth => ActualWidth;
        public double ViewportHeight => ActualHeight;

        public Point ScreenToCanvas(Point screenPoint)
        {
            var z = ZoomLevel;
            return new Point((screenPoint.X - _translate.X) / z, (screenPoint.Y - _translate.Y) / z);
        }

        public void SetOffset(double x, double y)
        {
            _translate.X = x;
            _translate.Y = y;
            RaiseViewportChanged();
        }

        /// <summary>
        /// 한 노드의 중심을 viewport 중심으로 보내는 translate 오프셋 계산. 줌은 변경 X.
        /// 디버그 흐름(BP/Continue/StepOver) 자동 스크롤의 핵심 수학 — UI 의존 없이 순수 함수로 단위 테스트 가능.
        /// 좌상단 (nodeX, nodeY) 의 노드 (nodeWidth × nodeHeight) 가 zoom 배율 화면 좌표 (nodeX*zoom + nodeWidth*zoom/2, ...) 에 있을 때,
        /// 그 점을 (viewportWidth/2, viewportHeight/2) 로 옮기는 translate.
        /// </summary>
        public static (double offsetX, double offsetY) ComputeCenteringOffset(
            double nodeX, double nodeY,
            double nodeWidth, double nodeHeight,
            double viewportWidth, double viewportHeight,
            double zoom)
        {
            var centerX = (nodeX + nodeWidth / 2.0) * zoom;
            var centerY = (nodeY + nodeHeight / 2.0) * zoom;
            return (viewportWidth / 2.0 - centerX, viewportHeight / 2.0 - centerY);
        }

        /// <summary>
        /// 지정 노드 중심으로 viewport 를 panning (줌 유지). viewport 가 아직 layout 안 됐으면 no-op.
        /// IsCurrentBreakpoint 가 true 로 바뀐 노드를 자동 추적할 때 호출.
        /// </summary>
        public void CenterOnNode(ViewModels.NodeViewModel nvm)
        {
            if (nvm == null) return;
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            var (ox, oy) = ComputeCenteringOffset(
                nvm.X, nvm.Y,
                NodeApproxWidthForBounds, NodeApproxHeightForBounds,
                ActualWidth, ActualHeight,
                ZoomLevel);
            SetOffset(ox, oy);
        }

        public void SetZoom(double zoom)
        {
            var pivot = new Point(ActualWidth / 2, ActualHeight / 2);
            ZoomAroundPivot(pivot, Clamp(zoom, MinZoom, MaxZoom));
        }

        public void FitToContent()
        {
            // 노드 위치 + 가정 폭 200/높이 80을 union 처리.
            if (Graph == null || Graph.Nodes.Count == 0) return;

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            const double approxW = 200, approxH = 80;
            foreach (var n in Graph.Nodes)
            {
                if (n.X < minX) minX = n.X;
                if (n.Y < minY) minY = n.Y;
                if (n.X + approxW > maxX) maxX = n.X + approxW;
                if (n.Y + approxH > maxY) maxY = n.Y + approxH;
            }
            ContentWidth = maxX - minX;
            ContentHeight = maxY - minY;
            ZoomToBounds(new Rect(minX, minY, ContentWidth, ContentHeight));
        }

        public void ZoomToBounds(Rect bounds, double padding = 0.8)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            var sx = (ActualWidth * padding) / bounds.Width;
            var sy = (ActualHeight * padding) / bounds.Height;
            var z = Clamp(Math.Min(sx, sy), MinZoom, MaxZoom);
            _scale.ScaleX = z;
            _scale.ScaleY = z;
            var centerX = bounds.X + bounds.Width / 2;
            var centerY = bounds.Y + bounds.Height / 2;
            _translate.X = ActualWidth / 2 - centerX * z;
            _translate.Y = ActualHeight / 2 - centerY * z;
            RaiseViewportChanged();
        }

        public event EventHandler ViewportChanged;
        private void RaiseViewportChanged() => ViewportChanged?.Invoke(this, EventArgs.Empty);

        #endregion
    }
}
