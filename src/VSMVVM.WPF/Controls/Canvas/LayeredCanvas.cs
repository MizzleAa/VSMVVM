using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using VSMVVM.WPF.Controls.Tools;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 개별 레이어를 나타내는 Canvas. LayeredCanvas의 자식으로 사용됩니다.
    /// </summary>
    public class CanvasLayer : Canvas
    {
        public CanvasLayer()
        {
            ClipToBounds = true;
        }
        #region DependencyProperties

        public static readonly DependencyProperty LayerNameProperty =
            DependencyProperty.Register(
                nameof(LayerName),
                typeof(string),
                typeof(CanvasLayer),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ZOrderProperty =
            DependencyProperty.Register(
                nameof(ZOrder),
                typeof(int),
                typeof(CanvasLayer),
                new PropertyMetadata(0, OnZOrderChanged));

        public string LayerName
        {
            get => (string)GetValue(LayerNameProperty);
            set => SetValue(LayerNameProperty, value);
        }

        public int ZOrder
        {
            get => (int)GetValue(ZOrderProperty);
            set => SetValue(ZOrderProperty, value);
        }

        #endregion

        #region Private Methods

        private static void OnZOrderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CanvasLayer layer)
            {
                Canvas.SetZIndex(layer, (int)e.NewValue);
            }
        }

        #endregion
    }

    /// <summary>
    /// 레이어 기반 캔버스 컨트롤.
    /// 포토샵 스타일: 레이어/도형 선택, 이동, 크기조절을 직접 관리합니다.
    /// 자체적으로 마우스 휠 줌/드래그 팬을 처리하며 <see cref="IZoomPanViewport"/>를 구현해
    /// MiniMap 등 추종 컨트롤이 바인딩할 수 있다.
    /// </summary>
    public class LayeredCanvas : Canvas, IZoomPanViewport
    {
        #region Fields

        private bool _isDraggingElement;
        private Point _dragStartCanvasPos;
        private double _dragOriginalLeft;
        private double _dragOriginalTop;
        private CanvasSelectionAdorner? _currentAdorner;

        // 그리기 세션 동안 유지되는 컨텍스트 (MouseDown → MouseUp)
        private CanvasToolContext? _currentToolContext;

        // Zoom/Pan 상태
        private readonly ScaleTransform _scaleTransform = new ScaleTransform(1.0, 1.0);
        private readonly TranslateTransform _translateTransform = new TranslateTransform();
        private readonly TransformGroup _transformGroup = new TransformGroup();
        private bool _isPanning;
        private Point _panStart;
        private bool _suppressDpSync;
        private bool _suppressZoomPivot; // ZoomLevel DP change 콜백의 pivot 보정을 우회하는 플래그 (휠 경로용)
        private bool _fitRetryScheduled;

        /// <summary>
        /// 현재 드래그 진행 중인지 여부.
        /// </summary>
        public bool IsDragging => _isDraggingElement;

        #endregion

        #region DependencyProperties

        /// <summary>
        /// 현재 선택된 요소 (CanvasLayer 또는 Shape).
        /// </summary>
        public static readonly DependencyProperty SelectedElementProperty =
            DependencyProperty.Register(
                nameof(SelectedElement),
                typeof(UIElement),
                typeof(LayeredCanvas),
                new PropertyMetadata(null, OnSelectedElementChanged));

        /// <summary>
        /// 선택 요소 변경 시 발생하는 이벤트. Canvas → 외부(ViewModel) 동기화용.
        /// </summary>
        public event System.EventHandler<UIElement?>? SelectionChanged;

        private static void OnSelectedElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LayeredCanvas lc)
            {
                lc.SelectionChanged?.Invoke(lc, e.NewValue as UIElement);
            }
        }

        public UIElement? SelectedElement
        {
            get => (UIElement?)GetValue(SelectedElementProperty);
            set => SetValue(SelectedElementProperty, value);
        }

        /// <summary>
        /// 현재 활성 도구.
        /// </summary>
        public static readonly DependencyProperty CurrentToolProperty =
            DependencyProperty.Register(
                nameof(CurrentTool),
                typeof(ICanvasTool),
                typeof(LayeredCanvas),
                new PropertyMetadata(null, OnCurrentToolChanged));

        private static void OnCurrentToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LayeredCanvas lc) return;
            var newTool = e.NewValue as ICanvasTool;
            // Select 모드일 때만 MaskLayer/MeasurementLayer 에 hit-test 허용.
            bool selectMode = newTool == null || newTool.Mode == CanvasToolMode.Select;
            foreach (UIElement child in lc.Children)
            {
                if (child is MaskLayer mask)
                    mask.IsHitTestVisible = selectMode;
                else if (child is MeasurementLayer ml)
                    ml.IsHitTestVisible = selectMode;
            }
        }

        public ICanvasTool? CurrentTool
        {
            get => (ICanvasTool?)GetValue(CurrentToolProperty);
            set => SetValue(CurrentToolProperty, value);
        }

        /// <summary>
        /// 그리기 완료 시 발생하는 이벤트.
        /// </summary>
        public event EventHandler? DrawingCompleted;

        internal void RaiseDrawingCompleted()
        {
            DrawingCompleted?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Zoom/Pan DependencyProperties

        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register(
                nameof(ZoomLevel),
                typeof(double),
                typeof(LayeredCanvas),
                new PropertyMetadata(1.0, OnZoomLevelChanged));

        public static readonly DependencyProperty MinZoomProperty =
            DependencyProperty.Register(
                nameof(MinZoom),
                typeof(double),
                typeof(LayeredCanvas),
                new PropertyMetadata(0.1));

        public static readonly DependencyProperty MaxZoomProperty =
            DependencyProperty.Register(
                nameof(MaxZoom),
                typeof(double),
                typeof(LayeredCanvas),
                new PropertyMetadata(20.0));

        public static readonly DependencyProperty OffsetXProperty =
            DependencyProperty.Register(
                nameof(OffsetX),
                typeof(double),
                typeof(LayeredCanvas),
                new PropertyMetadata(0.0, OnOffsetXChanged));

        public static readonly DependencyProperty OffsetYProperty =
            DependencyProperty.Register(
                nameof(OffsetY),
                typeof(double),
                typeof(LayeredCanvas),
                new PropertyMetadata(0.0, OnOffsetYChanged));

        public static readonly DependencyProperty ContentWidthProperty =
            DependencyProperty.Register(
                nameof(ContentWidth),
                typeof(double),
                typeof(LayeredCanvas),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty ContentHeightProperty =
            DependencyProperty.Register(
                nameof(ContentHeight),
                typeof(double),
                typeof(LayeredCanvas),
                new PropertyMetadata(0.0));

        public double ZoomLevel
        {
            get => (double)GetValue(ZoomLevelProperty);
            set => SetValue(ZoomLevelProperty, value);
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

        public double OffsetX
        {
            get => (double)GetValue(OffsetXProperty);
            set => SetValue(OffsetXProperty, value);
        }

        public double OffsetY
        {
            get => (double)GetValue(OffsetYProperty);
            set => SetValue(OffsetYProperty, value);
        }

        public double ContentWidth
        {
            get => (double)GetValue(ContentWidthProperty);
            set => SetValue(ContentWidthProperty, value);
        }

        public double ContentHeight
        {
            get => (double)GetValue(ContentHeightProperty);
            set => SetValue(ContentHeightProperty, value);
        }

        /// <summary>Zoom 또는 Pan 상태가 바뀔 때마다 발화한다.</summary>
        public event EventHandler? ViewportChanged;

        #endregion

        #region Constructor

        public LayeredCanvas()
        {
            ClipToBounds = true; // LayeredCanvas 자체가 뷰포트 — 자식(이미지)이 transform으로 커지면 여기서 clip.
            SnapsToDevicePixels = true;
            Background = Brushes.Transparent; // hit-test 활성화

            // 마스터 transform 상태 — 자식에 직접 공유하지 않는다.
            // WPF Freezable 복제 이슈로 인해 각 자식에 별도 인스턴스를 부여하고
            // ApplyTransformToChildren() 로 브로드캐스트 동기화한다.
            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);

            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            VerticalAlignment = System.Windows.VerticalAlignment.Stretch;

            // XAML 파싱 시점의 OnVisualChildrenChanged 호출 타이밍 이슈(자식 RenderTransform 이
            // Identity 로 덮여있는 상태로 남는 케이스)를 방지하기 위해 Loaded 시점에 1회 강제 동기화.
            Loaded += (_, __) => ApplyTransformToChildren();
        }

        /// <summary>
        /// 뷰포트 역할 — 부모가 주는 constraint를 그대로 채택해 Border를 꽉 채운다.
        /// 자식들은 Canvas.Left/Top + Width/Height로 pre-transform 좌표에 배치되고,
        /// 각자의 RenderTransform이 시각적 zoom/pan을 담당한다.
        /// </summary>
        protected override Size MeasureOverride(Size constraint)
        {
            foreach (UIElement child in InternalChildren)
                child?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double w = double.IsInfinity(constraint.Width) ? 0 : constraint.Width;
            double h = double.IsInfinity(constraint.Height) ? 0 : constraint.Height;
            return new Size(w, h);
        }

        /// <summary>
        /// 자식 추가 시 자식 전용 <see cref="MatrixTransform"/> 인스턴스를 부여한다.
        /// 이후 갱신은 인스턴스 교체가 아니라 해당 인스턴스의 <see cref="MatrixTransform.Matrix"/> 프로퍼티만 바꾼다 —
        /// 교체 중 일어날 수 있는 layout/render 불일치를 원천 차단.
        /// </summary>
        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
            if (visualAdded is UIElement added)
            {
                added.RenderTransform = new MatrixTransform(BuildMatrix());
            }
            if (visualRemoved is UIElement removed)
            {
                // 제거된 자식이 다른 곳에 재사용될 수 있으므로 transform 인스턴스 해제.
                removed.RenderTransform = Transform.Identity;
            }
        }

        /// <summary>현재 마스터 scale/translate로 이루어진 affine 행렬을 구성. row-vector convention: v' = v * M.</summary>
        private Matrix BuildMatrix()
        {
            double s = _scaleTransform.ScaleX;
            double tx = _translateTransform.X;
            double ty = _translateTransform.Y;
            // v * M = (v.x*s + tx, v.y*s + ty)
            return new Matrix(s, 0, 0, s, tx, ty);
        }

        /// <summary>모든 자식의 MatrixTransform.Matrix를 현재 마스터 행렬로 갱신.</summary>
        private void ApplyTransformToChildren()
        {
            var m = BuildMatrix();
            foreach (UIElement child in InternalChildren)
            {
                if (child == null) continue;
                if (child.RenderTransform is MatrixTransform mt)
                {
                    mt.Matrix = m;
                }
                else
                {
                    child.RenderTransform = new MatrixTransform(m);
                }
                // zoom 에 따라 OnRender 에서 pen 두께를 재계산하는 layer(MaskLayer 등) 는
                // RenderTransform 변경만으론 OnRender 가 재호출되지 않으므로 명시적 invalidate.
                if (child is MaskLayer ml)
                {
                    ml.InvalidateVisual();
                    // 붙어있는 Adorner 들도 재렌더(핸들/점선 두께 보정).
                    var adLayer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(ml);
                    var ads = adLayer?.GetAdorners(ml);
                    if (ads != null) foreach (var a in ads) a.InvalidateVisual();
                }
                else if (child is MeasurementLayer meas)
                {
                    meas.InvalidateVisual();
                }
                else if (child is ShapePreviewLayer shp)
                {
                    shp.InvalidateVisual();
                }
            }
        }

        #endregion

        #region Mouse Overrides — 선택/드래그/줌/팬

        /// <summary>
        /// 마우스 휠:
        ///   - Modifier 없음: 커서 pivot zoom
        ///   - Shift + 휠: 수직 pan (뷰포트 높이의 10%/notch)
        ///   - Ctrl + 휠: 수평 pan (뷰포트 폭의 10%/notch)
        /// Zoom 공식 유도: 뷰포트 좌표 vs = v * s + t. 커서 p 아래 픽셀이 고정되려면
        ///   (vs - p) 가 k배 확대되어야 → vs_new = (vs - p) * k + p = v*(s*k) + (t - p)*k + p
        ///   ⇒ s_new = s*k,  t_new = (t - p)*k + p
        /// </summary>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Handled) return;

            var mods = System.Windows.Input.Keyboard.Modifiers;
            bool shift = (mods & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift;
            bool ctrl = (mods & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control;

            // Shift 또는 Ctrl → pan, 없으면 기존 pivot zoom.
            if (shift || ctrl)
            {
                PanByWheel(e, horizontal: ctrl);
                e.Handled = true;
                return;
            }

            var pivot = e.GetPosition(this);
            var zoomFactor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            var currentZoom = _scaleTransform.ScaleX > 0 ? _scaleTransform.ScaleX : 1.0;
            var newZoom = currentZoom * zoomFactor;
            newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));
            var k = newZoom / currentZoom;
            if (!double.IsFinite(k) || k <= 0) { e.Handled = true; return; }

            double oldTx = _translateTransform.X;
            double oldTy = _translateTransform.Y;
            double newTx = (oldTx - pivot.X) * k + pivot.X;
            double newTy = (oldTy - pivot.Y) * k + pivot.Y;

            _scaleTransform.ScaleX = newZoom;
            _scaleTransform.ScaleY = newZoom;
            _translateTransform.X = newTx;
            _translateTransform.Y = newTy;

            ApplyTransformToChildren();

            // 휠은 커서 pivot 으로 translate 를 이미 정확히 계산했으므로,
            // DP change 콜백의 중심 pivot 재보정을 차단한다.
            _suppressZoomPivot = true;
            try { ZoomLevel = newZoom; }
            finally { _suppressZoomPivot = false; }
            SyncOffsetDpFromTransform();
            InvalidateAdorner();
            RaiseViewportChanged();
            e.Handled = true;
        }

        /// <summary>
        /// Shift/Ctrl + 휠 기반 pan. 한 notch(Delta=±120) 당 뷰포트 치수의 10% 이동.
        /// 휠 위(양수 Delta) → translate 양수 → 콘텐츠를 +방향으로 밀어 "위/왼쪽 영역" 을 보게 함 (표준 스크롤 관례).
        /// </summary>
        private void PanByWheel(MouseWheelEventArgs e, bool horizontal)
        {
            double notches = e.Delta / 120.0;
            double viewportExtent = horizontal ? ActualWidth : ActualHeight;
            if (viewportExtent <= 0) return;
            double step = notches * (viewportExtent * 0.1);
            if (!double.IsFinite(step) || step == 0) return;

            if (horizontal)
                SetTranslateSafe(_translateTransform.X + step, _translateTransform.Y);
            else
                SetTranslateSafe(_translateTransform.X, _translateTransform.Y + step);

            ApplyTransformToChildren();
            SyncOffsetDpFromTransform();
            InvalidateAdorner();
            RaiseViewportChanged();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.Handled) return;

            var tool = CurrentTool;

            // Arrow 도구: 모든 드래그 = Pan 전용.
            if (tool != null && tool.Mode == CanvasToolMode.Arrow)
            {
                StartPan(e);
                e.Handled = true;
                return;
            }

            // Select 모드: Shape hit → 기존 드래그, 빈 영역 + MaskLayer hit 아님이면 MaskLayer 가 처리하도록 bubble.
            // (MaskLayer.OnMouseLeftButtonDown 이 픽셀 인스턴스 hit / rubber band 담당).
            if (tool == null || tool.Mode == CanvasToolMode.Select)
            {
                var localPoint = ScreenToCanvas(e.GetPosition(this));
                var hitElement = FindHitElement(localPoint);

                if (hitElement != null)
                {
                    SelectElement(hitElement);
                    StartDrag(hitElement, e);
                    e.Handled = true;
                    return;
                }

                ClearSelection();
                // Pan 은 Arrow 도구 전담 — Select 모드 빈 영역은 MaskLayer rubber band 처리(이미 Handled 되었을 것).
                // e.Handled 가 이 시점에 여전히 false 면 그냥 return (아무 동작 없음).
                return;
            }

            // Interactive tool 이 진행 중인 세션을 갖고 있으면 기존 context 재사용 — 연속 클릭 지원.
            var interactive = tool as Tools.IInteractiveCanvasTool;
            if (_currentToolContext == null || interactive == null || !interactive.IsInputSessionActive)
            {
                _currentToolContext = CreateToolContext(e);
            }

            // Tool은 콘텐츠(pre-transform) 좌표계에서 동작하므로 역변환하여 전달.
            var pos = ScreenToCanvas(e.GetPosition(this));
            bool handled = tool.OnMouseDown(_currentToolContext, pos, e);
            if (handled)
            {
                if (CurrentTool == tool)
                    CaptureMouse();
                else
                    _currentToolContext = null;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Canvas 에는 OnMouseDoubleClick override 가 없어 PreviewMouseLeftButtonDown 에서 ClickCount 로 감지.
        /// MouseDown 기본 처리 전에 interactive tool 에 기회를 준다.
        /// </summary>
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            if (e.Handled) return;
            if (e.ClickCount >= 2
                && CurrentTool is Tools.IInteractiveCanvasTool itool
                && _currentToolContext != null)
            {
                var pos = ScreenToCanvas(e.GetPosition(this));
                if (itool.OnDoubleClick(_currentToolContext, pos, e))
                {
                    if (!itool.IsInputSessionActive)
                    {
                        _currentToolContext = null;
                        ReleaseMouseCapture();
                    }
                    e.Handled = true;
                }
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Handled) return;
            if (CurrentTool is Tools.IInteractiveCanvasTool itool && _currentToolContext != null)
            {
                bool handled = false;
                if (e.Key == Key.Enter || e.Key == Key.Return)
                    handled = itool.OnEnterPressed(_currentToolContext);
                else if (e.Key == Key.Escape)
                    handled = itool.OnEscapePressed(_currentToolContext);
                if (handled)
                {
                    if (!itool.IsInputSessionActive)
                    {
                        _currentToolContext = null;
                        ReleaseMouseCapture();
                    }
                    e.Handled = true;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Pan 처리 우선
            if (_isPanning)
            {
                var pos = e.GetPosition(this);
                SetTranslateSafe(
                    _translateTransform.X + (pos.X - _panStart.X),
                    _translateTransform.Y + (pos.Y - _panStart.Y));
                _panStart = pos;
                ApplyTransformToChildren();
                SyncOffsetDpFromTransform();
                InvalidateAdorner();
                RaiseViewportChanged();
                e.Handled = true;
                return;
            }

            var tool = CurrentTool;

            // Select 모드: 기존 드래그 로직
            if (tool == null || tool.Mode == CanvasToolMode.Select)
            {
                if (_isDraggingElement && SelectedElement != null)
                {
                    var parentCanvas = FindParentCanvas(SelectedElement);
                    var currentPos = parentCanvas != null
                        ? e.GetPosition(parentCanvas)
                        : e.GetPosition(this);

                    var deltaX = currentPos.X - _dragStartCanvasPos.X;
                    var deltaY = currentPos.Y - _dragStartCanvasPos.Y;

                    var newLeft = _dragOriginalLeft + deltaX;
                    var newTop = _dragOriginalTop + deltaY;

                    if (SelectedElement is FrameworkElement fe
                        && parentCanvas is CanvasLayer parentLayer)
                    {
                        var parentW = !double.IsNaN(parentLayer.Width) ? parentLayer.Width : parentLayer.ActualWidth;
                        var parentH = !double.IsNaN(parentLayer.Height) ? parentLayer.Height : parentLayer.ActualHeight;

                        if (!double.IsNaN(parentW) && !double.IsNaN(parentH))
                        {
                            var maxLeft = parentW - fe.ActualWidth;
                            var maxTop = parentH - fe.ActualHeight;
                            if (maxLeft > 0) newLeft = Math.Max(0, Math.Min(newLeft, maxLeft));
                            if (maxTop > 0) newTop = Math.Max(0, Math.Min(newTop, maxTop));
                        }
                    }

                    SetLeft(SelectedElement, newLeft);
                    SetTop(SelectedElement, newTop);

                    _currentAdorner?.InvalidateVisual();
                    e.Handled = true;
                }
                return;
            }

            // 그리기 도구 위임 (콘텐츠 좌표로 역변환)
            // Interactive tool 은 capture 없이도 hover 로 preview 갱신 받아야 함.
            bool allowHover = tool is Tools.IInteractiveCanvasTool itool && itool.IsInputSessionActive;
            if ((IsMouseCaptured || allowHover) && _currentToolContext != null)
            {
                tool.OnMouseMove(_currentToolContext, ScreenToCanvas(e.GetPosition(this)), e);
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (_isPanning)
            {
                _isPanning = false;
                ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

            var tool = CurrentTool;

            if (tool == null || tool.Mode == CanvasToolMode.Select)
            {
                if (_isDraggingElement)
                {
                    _isDraggingElement = false;
                    ReleaseMouseCapture();
                    e.Handled = true;
                }
                return;
            }

            if (IsMouseCaptured && _currentToolContext != null)
            {
                tool.OnMouseUp(_currentToolContext, ScreenToCanvas(e.GetPosition(this)), e);
                // Interactive tool 이 세션 유지를 원하면 context 보존 + capture 유지.
                if (tool is Tools.IInteractiveCanvasTool itool && itool.IsInputSessionActive)
                {
                    e.Handled = true;
                    return;
                }
                _currentToolContext = null;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void StartPan(MouseEventArgs e)
        {
            _panStart = e.GetPosition(this);
            _isPanning = true;
            CaptureMouse();
        }

        #endregion

        #region Public Methods — 선택/해제

        /// <summary>
        /// 요소를 선택합니다. adorner를 부착합니다.
        /// </summary>
        public void SelectElement(UIElement element)
        {
            if (element == null || element == SelectedElement)
                return;

            ClearSelection();
            SelectedElement = element;

            var adornerLayer = AdornerLayer.GetAdornerLayer(element);
            if (adornerLayer != null)
            {
                _currentAdorner = new CanvasSelectionAdorner(element);
                adornerLayer.Add(_currentAdorner);
            }
        }

        /// <summary>
        /// 현재 선택을 해제합니다.
        /// </summary>
        public void ClearSelection()
        {
            if (_currentAdorner != null && SelectedElement != null)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(SelectedElement);
                adornerLayer?.Remove(_currentAdorner);
                _currentAdorner = null;
            }

            SelectedElement = null;
        }

        /// <summary>
        /// adorner를 다시 그립니다 (zoom 변경 시 호출).
        /// </summary>
        public void InvalidateAdorner()
        {
            _currentAdorner?.InvalidateVisual();
        }

        #endregion

        #region Public Methods — 레이어 관리

        /// <summary>
        /// 이름으로 레이어를 찾습니다.
        /// </summary>
        public CanvasLayer? FindLayer(string layerName)
        {
            foreach (var child in Children)
            {
                if (child is CanvasLayer layer && layer.LayerName == layerName)
                    return layer;
            }
            return null;
        }

        /// <summary>
        /// 모든 CanvasLayer를 ZOrder 순으로 반환합니다.
        /// </summary>
        public IReadOnlyList<CanvasLayer> GetLayers()
        {
            var layers = new List<CanvasLayer>();
            foreach (var child in Children)
            {
                if (child is CanvasLayer layer)
                    layers.Add(layer);
            }
            return layers.OrderBy(l => l.ZOrder).ToList();
        }

        public bool MoveLayerUp(CanvasLayer layer)
        {
            var layers = GetLayers();
            var index = layers.ToList().IndexOf(layer);
            if (index < 0 || index >= layers.Count - 1)
                return false;

            var above = layers[index + 1];
            var temp = layer.ZOrder;
            layer.ZOrder = above.ZOrder;
            above.ZOrder = temp;

            if (layer.ZOrder == above.ZOrder)
                layer.ZOrder = above.ZOrder + 1;

            return true;
        }

        public bool MoveLayerDown(CanvasLayer layer)
        {
            var layers = GetLayers();
            var index = layers.ToList().IndexOf(layer);
            if (index <= 0)
                return false;

            var below = layers[index - 1];
            var temp = layer.ZOrder;
            layer.ZOrder = below.ZOrder;
            below.ZOrder = temp;

            if (layer.ZOrder == below.ZOrder)
                below.ZOrder = layer.ZOrder + 1;

            return true;
        }

        public void SetLayerVisibility(string layerName, bool isVisible)
        {
            var layer = FindLayer(layerName);
            if (layer != null)
                layer.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ClearLayer(string layerName)
        {
            var layer = FindLayer(layerName);
            layer?.Children.Clear();
        }

        #endregion

        #region Private Methods

        private void StartDrag(UIElement element, MouseButtonEventArgs e)
        {
            _isDraggingElement = true;

            var parentCanvas = FindParentCanvas(element);
            _dragStartCanvasPos = parentCanvas != null
                ? e.GetPosition(parentCanvas)
                : e.GetPosition(this);

            var left = GetLeft(element);
            var top = GetTop(element);
            _dragOriginalLeft = double.IsNaN(left) ? 0 : left;
            _dragOriginalTop = double.IsNaN(top) ? 0 : top;

            CaptureMouse();
        }

        /// <summary>
        /// 클릭 지점의 요소를 찾습니다.
        /// 우선순위: Shape → CanvasLayer 영역.
        /// </summary>
        private UIElement? FindHitElement(Point localPoint)
        {
            // 1차: 각 레이어 내부 도형 검색
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i] is CanvasLayer layer && layer.Visibility == Visibility.Visible)
                {
                    var layerLeft = GetLeft(layer);
                    var layerTop = GetTop(layer);
                    if (double.IsNaN(layerLeft)) layerLeft = 0;
                    if (double.IsNaN(layerTop)) layerTop = 0;

                    var layerLocalPoint = new Point(
                        localPoint.X - layerLeft,
                        localPoint.Y - layerTop);

                    var shapeHit = FindHitInCanvas(layer, layerLocalPoint);
                    if (shapeHit != null)
                        return shapeHit;
                }
            }

            // 2차: CanvasLayer 영역 자체
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i] is CanvasLayer layer && layer.Visibility == Visibility.Visible)
                {
                    var layerBounds = GetChildBounds(layer);
                    if (!layerBounds.IsEmpty && layerBounds.Contains(localPoint))
                        return layer;
                }
            }

            return null;
        }

        private static UIElement? FindHitInCanvas(Canvas canvas, Point localPoint)
        {
            for (int i = canvas.Children.Count - 1; i >= 0; i--)
            {
                var child = canvas.Children[i];
                if (child.Visibility != Visibility.Visible) continue;

                var bounds = GetChildBounds(child);
                if (!bounds.IsEmpty && bounds.Contains(localPoint))
                    return child;
            }
            return null;
        }

        internal static Rect GetChildBounds(UIElement child)
        {
            var left = GetLeft(child);
            var top = GetTop(child);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            double width, height;
            if (child is FrameworkElement fe)
            {
                width = fe.ActualWidth > 0 ? fe.ActualWidth : fe.Width;
                height = fe.ActualHeight > 0 ? fe.ActualHeight : fe.Height;
            }
            else
            {
                width = child.RenderSize.Width;
                height = child.RenderSize.Height;
            }

            if (double.IsNaN(width) || double.IsNaN(height))
                return Rect.Empty;

            return new Rect(left, top, width, height);
        }

        internal static Canvas? FindParentCanvas(UIElement element)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is Canvas c) return c;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        /// <summary>
        /// Tool에 전달할 컨텍스트를 생성합니다.
        /// </summary>
        private CanvasToolContext CreateToolContext(MouseEventArgs e)
        {
            return new CanvasToolContext(this, GetTargetLayer(), FindMaskLayer(), e);
        }

        /// <summary>
        /// 자식 중 첫 번째 <see cref="MaskLayer"/>를 반환. 없으면 null.
        /// </summary>
        private MaskLayer? FindMaskLayer()
        {
            foreach (UIElement child in Children)
            {
                if (child is MaskLayer m) return m;
            }
            return null;
        }

        /// <summary>
        /// 그리기 대상 레이어를 반환합니다. 선택된 레이어 우선, 없으면 마지막 레이어.
        /// </summary>
        private CanvasLayer? GetTargetLayer()
        {
            if (SelectedElement is CanvasLayer selected)
                return selected;

            // 마지막 레이어
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i] is CanvasLayer layer)
                    return layer;
            }
            return null;
        }

        #endregion

        #region Public Methods — Zoom/Pan/Fit

        /// <summary>모든 자식 콘텐츠를 뷰포트(Border)에 맞춰 zoom + center.</summary>
        public void FitToContent()
        {
            if (Children.Count == 0) return;

            if (ActualWidth <= 0 || ActualHeight <= 0)
            {
                if (!_fitRetryScheduled)
                {
                    _fitRetryScheduled = true;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _fitRetryScheduled = false;
                        FitToContent();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                return;
            }

            var bounds = GetChildrenBounds();
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;

            ContentWidth = bounds.Width;
            ContentHeight = bounds.Height;

            FitRectInternal(bounds, padding: 0.9);
        }

        /// <summary>
        /// 주어진 캔버스 좌표계의 사각형을 뷰포트 중앙에 맞춰 zoom + center.
        /// FitToContent 와 동일한 로직이지만 bounds 를 외부에서 지정. ContentWidth/Height 는 건드리지 않는다
        /// (fit 기준은 전체 콘텐츠이고 zoom-to-region 은 임시 포커스이므로).
        /// </summary>
        public void ZoomToBounds(Rect bounds, double padding = 0.8)
        {
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            FitRectInternal(bounds, padding);
        }

        private void FitRectInternal(Rect bounds, double padding)
        {
            var viewportWidth = ActualWidth;
            var viewportHeight = ActualHeight;

            var scaleX = viewportWidth / bounds.Width;
            var scaleY = viewportHeight / bounds.Height;
            var zoom = Math.Min(scaleX, scaleY) * padding;
            zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));

            SetScaleSafe(zoom);

            var contentCenterX = bounds.X + bounds.Width / 2;
            var contentCenterY = bounds.Y + bounds.Height / 2;
            SetTranslateSafe(
                viewportWidth / 2 - contentCenterX * zoom,
                viewportHeight / 2 - contentCenterY * zoom);

            ApplyTransformToChildren();
            _suppressZoomPivot = true;
            try { ZoomLevel = zoom; }
            finally { _suppressZoomPivot = false; }
            SyncOffsetDpFromTransform();
            InvalidateAdorner();
            RaiseViewportChanged();
        }

        /// <summary>Zoom과 Pan을 초기 상태(1.0, 0, 0)로 리셋.</summary>
        public void ResetView()
        {
            SetScaleSafe(1.0);
            SetTranslateSafe(0, 0);
            ApplyTransformToChildren();
            _suppressZoomPivot = true;
            try { ZoomLevel = 1.0; }
            finally { _suppressZoomPivot = false; }
            SyncOffsetDpFromTransform();
            InvalidateAdorner();
            RaiseViewportChanged();
        }

        /// <summary>뷰포트(Border) 좌표를 콘텐츠(캔버스) 좌표로 역변환.</summary>
        public Point ScreenToCanvas(Point screenPoint)
        {
            var scale = _scaleTransform.ScaleX > 0 ? _scaleTransform.ScaleX : 1.0;
            var x = (screenPoint.X - _translateTransform.X) / scale;
            var y = (screenPoint.Y - _translateTransform.Y) / scale;
            return new Point(x, y);
        }

        /// <summary>콘텐츠(캔버스) 좌표를 뷰포트(Border) 좌표로 변환.</summary>
        public Point CanvasToScreen(Point canvasPoint)
        {
            var scale = _scaleTransform.ScaleX > 0 ? _scaleTransform.ScaleX : 1.0;
            return new Point(canvasPoint.X * scale + _translateTransform.X,
                             canvasPoint.Y * scale + _translateTransform.Y);
        }

        #endregion

        #region IZoomPanViewport

        double IZoomPanViewport.ViewportWidth => ActualWidth;
        double IZoomPanViewport.ViewportHeight => ActualHeight;

        void IZoomPanViewport.SetOffset(double x, double y)
        {
            SetTranslateSafe(x, y);
            ApplyTransformToChildren();
            SyncOffsetDpFromTransform();
            InvalidateAdorner();
            RaiseViewportChanged();
        }

        void IZoomPanViewport.SetZoom(double zoom)
        {
            if (!double.IsFinite(zoom) || zoom <= 0) return;
            var clamped = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
            // ZoomLevel DP 할당 → OnZoomLevelChanged 가 뷰포트 중심 pivot 으로 translate 보정.
            ZoomLevel = clamped;
        }

        event EventHandler? IZoomPanViewport.ViewportChanged
        {
            add => ViewportChanged += value;
            remove => ViewportChanged -= value;
        }

        #endregion

        #region Private — Zoom/Pan helpers

        private Rect GetChildrenBounds()
        {
            var result = Rect.Empty;
            foreach (UIElement child in Children)
            {
                var bounds = GetChildBounds(child);
                if (!bounds.IsEmpty)
                    result = result.IsEmpty ? bounds : Rect.Union(result, bounds);
            }
            return result;
        }

        /// <summary>
        /// ZoomLevel DP 변경 시 뷰포트 중심을 pivot 으로 잡아 translate 를 보정한다.
        /// 휠/Fit/Reset 경로는 자체 pivot 으로 translate 를 이미 계산했으므로 <see cref="_suppressZoomPivot"/>
        /// 플래그로 이 재보정을 우회한다.
        /// 공식: vs_new = (vs - p)*k + p,  t_new = (t - p)*k + p  (k = newZoom/oldZoom, p = viewport center)
        /// </summary>
        private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LayeredCanvas lc) return;

            var newZoom = (double)e.NewValue;
            if (!double.IsFinite(newZoom) || newZoom <= 0) return;

            // 휠/Fit/Reset 등 이미 translate 까지 설정 완료한 경로 → scale 만 맞추고 조기 반환.
            if (lc._suppressZoomPivot)
            {
                lc._scaleTransform.ScaleX = newZoom;
                lc._scaleTransform.ScaleY = newZoom;
                return;
            }

            var oldZoom = (double)e.OldValue;
            if (oldZoom <= 0) oldZoom = 1.0;
            var k = newZoom / oldZoom;
            if (!double.IsFinite(k) || k <= 0) return;

            var pivotX = lc.ActualWidth * 0.5;
            var pivotY = lc.ActualHeight * 0.5;
            var tx = lc._translateTransform.X;
            var ty = lc._translateTransform.Y;

            lc._scaleTransform.ScaleX = newZoom;
            lc._scaleTransform.ScaleY = newZoom;
            lc._translateTransform.X = (tx - pivotX) * k + pivotX;
            lc._translateTransform.Y = (ty - pivotY) * k + pivotY;

            lc.ApplyTransformToChildren();
            lc.SyncOffsetDpFromTransform();
            lc.InvalidateAdorner();
            lc.RaiseViewportChanged();
        }

        private static void OnOffsetXChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LayeredCanvas lc && !lc._suppressDpSync)
            {
                lc.SetTranslateSafe((double)e.NewValue, lc._translateTransform.Y);
                lc.ApplyTransformToChildren();
                lc.InvalidateAdorner();
                lc.RaiseViewportChanged();
            }
        }

        private static void OnOffsetYChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LayeredCanvas lc && !lc._suppressDpSync)
            {
                lc.SetTranslateSafe(lc._translateTransform.X, (double)e.NewValue);
                lc.ApplyTransformToChildren();
                lc.InvalidateAdorner();
                lc.RaiseViewportChanged();
            }
        }

        private void SyncOffsetDpFromTransform()
        {
            _suppressDpSync = true;
            try
            {
                if (OffsetX != _translateTransform.X) SetValue(OffsetXProperty, _translateTransform.X);
                if (OffsetY != _translateTransform.Y) SetValue(OffsetYProperty, _translateTransform.Y);
            }
            finally { _suppressDpSync = false; }
        }

        private void RaiseViewportChanged() => ViewportChanged?.Invoke(this, EventArgs.Empty);

        private void SetTranslateSafe(double x, double y)
        {
            const double LIMIT = 1e7;
            if (!double.IsFinite(x)) x = 0;
            if (!double.IsFinite(y)) y = 0;
            if (x < -LIMIT) x = -LIMIT; else if (x > LIMIT) x = LIMIT;
            if (y < -LIMIT) y = -LIMIT; else if (y > LIMIT) y = LIMIT;
            _translateTransform.X = x;
            _translateTransform.Y = y;
            // broadcast는 호출자가 담당 — 여러 상태를 원자적으로 바꾸기 위함.
        }

        private void SetScaleSafe(double scale)
        {
            if (!double.IsFinite(scale) || scale <= 0) scale = 1.0;
            _scaleTransform.ScaleX = scale;
            _scaleTransform.ScaleY = scale;
        }

        #endregion
    }
}
