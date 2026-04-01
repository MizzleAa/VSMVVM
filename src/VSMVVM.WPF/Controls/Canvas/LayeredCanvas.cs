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
    /// ImageCanvas(줌/팬 뷰포트)의 자식으로 배치됩니다.
    /// </summary>
    public class LayeredCanvas : Canvas
    {
        #region Fields

        private bool _isDraggingElement;
        private Point _dragStartCanvasPos;
        private double _dragOriginalLeft;
        private double _dragOriginalTop;
        private CanvasSelectionAdorner? _currentAdorner;

        // 그리기 세션 동안 유지되는 컨텍스트 (MouseDown → MouseUp)
        private CanvasToolContext? _currentToolContext;

        /// <summary>
        /// 현재 드래그 진행 중인지 여부. ImageCanvas에서 panning 차단 용도.
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
                new PropertyMetadata(null));

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

        #region Constructor

        public LayeredCanvas()
        {
            ClipToBounds = true;
            SnapsToDevicePixels = true;
            Background = Brushes.Transparent; // hit-test 활성화
        }

        #endregion

        #region Mouse Overrides — 선택/드래그

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            var tool = CurrentTool;

            // Select 모드 또는 Tool 없음 → 기존 선택/드래그 로직
            if (tool == null || tool.Mode == CanvasToolMode.Select)
            {
                var localPoint = e.GetPosition(this);
                var hitElement = FindHitElement(localPoint);

                if (hitElement != null)
                {
                    SelectElement(hitElement);
                    StartDrag(hitElement, e);
                    e.Handled = true;
                }
                else
                {
                    ClearSelection();
                }
                return;
            }

            // Ctrl 누름 → Pan 패스스루
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                return;

            // Context를 MouseDown 시점에 캐시 (세션 동안 TargetLayer 고정)
            _currentToolContext = CreateToolContext(e);
            var pos = e.GetPosition(this);
            bool handled = tool.OnMouseDown(_currentToolContext, pos, e);
            if (handled)
            {
                // 도구가 OnMouseDown 내에서 전환된 경우 (예: ImageTool → Select)
                // CaptureMouse 하지 않음 — Move/Up 세션이 필요 없음
                if (CurrentTool == tool)
                {
                    CaptureMouse();
                }
                else
                {
                    _currentToolContext = null;
                }
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

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

            // 그리기 도구 위임 (캐시된 컨텍스트 사용)
            if (IsMouseCaptured && _currentToolContext != null)
            {
                tool.OnMouseMove(_currentToolContext, e.GetPosition(this), e);
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            var tool = CurrentTool;

            // Select 모드: 기존 로직
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

            // 그리기 도구 위임 (캐시된 컨텍스트 사용)
            if (IsMouseCaptured && _currentToolContext != null)
            {
                tool.OnMouseUp(_currentToolContext, e.GetPosition(this), e);
                _currentToolContext = null; // 세션 종료
                ReleaseMouseCapture();
                e.Handled = true;
            }
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
            return new CanvasToolContext(this, GetTargetLayer(), e);
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
    }
}
