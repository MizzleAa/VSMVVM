using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// Zoom/Pan 기반 이미지 캔버스 컨트롤.
    /// 마우스 휠 줌, 드래그 팬, 자식 요소 선택/이동/크기조절을 지원합니다.
    /// LayeredCanvas 내부 도형도 개별 선택 가능.
    /// </summary>
    public class ImageCanvas : Canvas
    {
        #region Fields

        private readonly ScaleTransform _scaleTransform = new ScaleTransform(1.0, 1.0);
        private readonly TranslateTransform _translateTransform = new TranslateTransform();
        private readonly TransformGroup _transformGroup = new TransformGroup();
        private Point _lastMousePosition;
        private bool _isPanning;
        private bool _isDraggingElement;
        private Point _dragStartCanvasPos;
        private double _dragOriginalLeft;
        private double _dragOriginalTop;
        private CanvasSelectionAdorner? _currentAdorner;

        #endregion

        #region DependencyProperties

        /// <summary>
        /// Zoom 레벨 종속성 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register(
                nameof(ZoomLevel),
                typeof(double),
                typeof(ImageCanvas),
                new PropertyMetadata(1.0, OnZoomLevelChanged));

        /// <summary>
        /// 최소 Zoom 레벨.
        /// </summary>
        public static readonly DependencyProperty MinZoomProperty =
            DependencyProperty.Register(
                nameof(MinZoom),
                typeof(double),
                typeof(ImageCanvas),
                new PropertyMetadata(0.1));

        /// <summary>
        /// 최대 Zoom 레벨.
        /// </summary>
        public static readonly DependencyProperty MaxZoomProperty =
            DependencyProperty.Register(
                nameof(MaxZoom),
                typeof(double),
                typeof(ImageCanvas),
                new PropertyMetadata(20.0));

        /// <summary>
        /// 현재 선택된 자식 요소.
        /// </summary>
        public static readonly DependencyProperty SelectedElementProperty =
            DependencyProperty.Register(
                nameof(SelectedElement),
                typeof(UIElement),
                typeof(ImageCanvas),
                new PropertyMetadata(null));

        /// <summary>
        /// 선택/드래그 기능 활성화 여부.
        /// </summary>
        public static readonly DependencyProperty IsSelectionEnabledProperty =
            DependencyProperty.Register(
                nameof(IsSelectionEnabled),
                typeof(bool),
                typeof(ImageCanvas),
                new PropertyMetadata(true));

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

        public UIElement? SelectedElement
        {
            get => (UIElement?)GetValue(SelectedElementProperty);
            set => SetValue(SelectedElementProperty, value);
        }

        public bool IsSelectionEnabled
        {
            get => (bool)GetValue(IsSelectionEnabledProperty);
            set => SetValue(IsSelectionEnabledProperty, value);
        }

        #endregion

        #region Constructor

        public ImageCanvas()
        {
            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);
            RenderTransform = _transformGroup;
            ClipToBounds = true;
        }

        #endregion

        #region Overrides

        /// <summary>
        /// 마우스 휠로 줌을 수행합니다. 마우스 위치 기준 pivot zoom.
        /// </summary>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            var position = e.GetPosition(this.Parent as IInputElement);
            var zoomFactor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            var newZoom = ZoomLevel * zoomFactor;

            newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));

            var canvasX = (position.X - _translateTransform.X) / _scaleTransform.ScaleX;
            var canvasY = (position.Y - _translateTransform.Y) / _scaleTransform.ScaleY;

            _scaleTransform.ScaleX = newZoom;
            _scaleTransform.ScaleY = newZoom;

            _translateTransform.X = position.X - canvasX * newZoom;
            _translateTransform.Y = position.Y - canvasY * newZoom;

            ZoomLevel = newZoom;
            _currentAdorner?.InvalidateVisual();

            e.Handled = true;
        }

        /// <summary>
        /// 마우스 다운: 자식 요소 위 → 선택 + 드래그, 빈 공간 → 팬.
        /// 그룹 우선 선택: LayeredCanvas 첫 클릭 → 그룹 선택, 이미 선택된 상태에서 재클릭 → 내부 도형 선택.
        /// </summary>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (IsSelectionEnabled)
            {
                var canvasPoint = e.GetPosition(this);

                // 1단계: 직접 자식 level에서 HitTest (LayeredCanvas 포함)
                var directHit = FindDirectHitChild(canvasPoint);

                if (directHit != null)
                {
                    // LayeredCanvas인 경우: 컨테이너 자체는 선택 불가 → 항상 내부 레이어로 드릴다운
                    if (directHit is LayeredCanvas layered)
                    {
                        var innerHit = FindHitInLayeredCanvas(layered, canvasPoint);
                        if (innerHit != null)
                        {
                            SelectElement(innerHit);
                            StartDrag(innerHit, e);
                            return;
                        }
                        // 내부 hit 없으면 선택 해제 (컨테이너 자체는 선택하지 않음)
                        ClearSelection();
                        return;
                    }

                    // 일반 자식 요소
                    SelectElement(directHit);
                    StartDrag(directHit, e);
                    return;
                }
                else
                {
                    ClearSelection();
                }
            }

            _isPanning = true;
            _lastMousePosition = e.GetPosition(Parent as IInputElement);
            CaptureMouse();
        }

        /// <summary>
        /// 드래그 모드 시작 헬퍼.
        /// </summary>
        private void StartDrag(UIElement element, MouseButtonEventArgs e)
        {
            _isDraggingElement = true;

            var parentCanvas = FindParentCanvas(element);
            _dragStartCanvasPos = parentCanvas != null && parentCanvas != this
                ? e.GetPosition(parentCanvas)
                : e.GetPosition(this);

            var left = GetLeft(element);
            var top = GetTop(element);
            _dragOriginalLeft = double.IsNaN(left) ? 0 : left;
            _dragOriginalTop = double.IsNaN(top) ? 0 : top;

            CaptureMouse();
            e.Handled = true;
        }

        /// <summary>
        /// 마우스 이동: 드래그 또는 팬.
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDraggingElement && SelectedElement != null)
            {
                // 부모 Canvas 기준으로 드래그 좌표 계산
                var parentCanvas = FindParentCanvas(SelectedElement);
                var currentPos = parentCanvas != null
                    ? e.GetPosition(parentCanvas)
                    : e.GetPosition(this);

                var deltaX = currentPos.X - _dragStartCanvasPos.X;
                var deltaY = currentPos.Y - _dragStartCanvasPos.Y;

                var newLeft = _dragOriginalLeft + deltaX;
                var newTop = _dragOriginalTop + deltaY;

                // 부모가 CanvasLayer인 경우 bounds 클램핑
                if (parentCanvas is CanvasLayer parentLayer
                    && SelectedElement is FrameworkElement fe)
                {
                    var maxLeft = parentLayer.Width - fe.ActualWidth;
                    var maxTop = parentLayer.Height - fe.ActualHeight;
                    if (!double.IsNaN(maxLeft) && maxLeft > 0)
                        newLeft = Math.Max(0, Math.Min(newLeft, maxLeft));
                    if (!double.IsNaN(maxTop) && maxTop > 0)
                        newTop = Math.Max(0, Math.Min(newTop, maxTop));
                }

                SetLeft(SelectedElement, newLeft);
                SetTop(SelectedElement, newTop);

                _currentAdorner?.InvalidateVisual();
                return;
            }

            if (_isPanning)
            {
                var currentPosition = e.GetPosition(Parent as IInputElement);
                var delta = currentPosition - _lastMousePosition;

                _translateTransform.X += delta.X;
                _translateTransform.Y += delta.Y;

                _lastMousePosition = currentPosition;
            }
        }

        /// <summary>
        /// 마우스 업: 드래그/팬 종료.
        /// </summary>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (_isDraggingElement)
            {
                _isDraggingElement = false;
                ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

            if (_isPanning)
            {
                _isPanning = false;
                ReleaseMouseCapture();
            }
        }

        // OnMouseLeave/Enter에서 adorner 숨김을 하지 않습니다.
        // Canvas 자식 요소 경계에서 Leave/Enter가 반복 발생하여 깜빡임을 유발하기 때문입니다.
        // 대신 부모 Border의 ClipToBounds="True"로 시각적 클리핑을 처리합니다.

        #endregion

        #region Public Methods

        /// <summary>
        /// 자식 요소를 선택합니다. 선택 adorner를 부착합니다.
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
        /// 모든 자식 요소를 뷰포트에 맞추는 Zoom Fit 기능.
        /// </summary>
        public void FitToContent()
        {
            if (Children.Count == 0) return;

            var parent = Parent as FrameworkElement;
            if (parent == null || parent.ActualWidth <= 0 || parent.ActualHeight <= 0) return;

            var bounds = GetContentBounds();
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;

            var viewportWidth = parent.ActualWidth;
            var viewportHeight = parent.ActualHeight;

            var scaleX = viewportWidth / bounds.Width;
            var scaleY = viewportHeight / bounds.Height;
            var zoom = Math.Min(scaleX, scaleY) * 0.9; // 90% fit with margin

            zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));

            _scaleTransform.ScaleX = zoom;
            _scaleTransform.ScaleY = zoom;

            // 콘텐츠를 뷰포트 중앙에 배치
            var contentCenterX = bounds.X + bounds.Width / 2;
            var contentCenterY = bounds.Y + bounds.Height / 2;

            _translateTransform.X = viewportWidth / 2 - contentCenterX * zoom;
            _translateTransform.Y = viewportHeight / 2 - contentCenterY * zoom;

            ZoomLevel = zoom;
            _currentAdorner?.InvalidateVisual();
        }

        /// <summary>
        /// 화면 좌표를 캔버스 좌표로 변환합니다.
        /// </summary>
        public Point ScreenToCanvas(Point screenPoint)
        {
            var x = (screenPoint.X - _translateTransform.X) / _scaleTransform.ScaleX;
            var y = (screenPoint.Y - _translateTransform.Y) / _scaleTransform.ScaleY;
            return new Point(x, y);
        }

        /// <summary>
        /// 캔버스 좌표를 화면 좌표로 변환합니다.
        /// </summary>
        public Point CanvasToScreen(Point canvasPoint)
        {
            var x = canvasPoint.X * _scaleTransform.ScaleX + _translateTransform.X;
            var y = canvasPoint.Y * _scaleTransform.ScaleY + _translateTransform.Y;
            return new Point(x, y);
        }

        /// <summary>
        /// Zoom과 Pan을 초기 상태로 리셋합니다.
        /// </summary>
        public void ResetView()
        {
            _scaleTransform.ScaleX = 1.0;
            _scaleTransform.ScaleY = 1.0;
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
            ZoomLevel = 1.0;
            ClearSelection();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 직접 자식 요소만 HitTest합니다 (LayeredCanvas 내부로 드릴다운하지 않음).
        /// </summary>
        private UIElement? FindDirectHitChild(Point canvasPoint)
        {
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                var child = Children[i];
                if (child.Visibility != Visibility.Visible)
                    continue;

                var childBounds = GetChildBounds(child);
                if (!childBounds.IsEmpty && childBounds.Contains(canvasPoint))
                    return child;
            }
            return null;
        }

        /// <summary>
        /// LayeredCanvas 내부에서 HitTest를 수행합니다.
        /// </summary>
        private UIElement? FindHitInLayeredCanvas(LayeredCanvas layered, Point imageCanvasPoint)
        {
            var layeredLeft = GetLeft(layered);
            var layeredTop = GetTop(layered);
            if (double.IsNaN(layeredLeft)) layeredLeft = 0;
            if (double.IsNaN(layeredTop)) layeredTop = 0;

            // ImageCanvas 좌표 → LayeredCanvas 로컬 좌표
            var localPoint = new Point(
                imageCanvasPoint.X - layeredLeft,
                imageCanvasPoint.Y - layeredTop);

            // 각 레이어를 높은 ZOrder부터 탐색
            // 1차: 내부 도형 hit 검색 (CanvasLayer offset 차감 필요)
            for (int i = layered.Children.Count - 1; i >= 0; i--)
            {
                if (layered.Children[i] is CanvasLayer layer && layer.Visibility == Visibility.Visible)
                {
                    var layerLeft = GetLeft(layer);
                    var layerTop = GetTop(layer);
                    if (double.IsNaN(layerLeft)) layerLeft = 0;
                    if (double.IsNaN(layerTop)) layerTop = 0;

                    // LayeredCanvas 좌표 → CanvasLayer 로컬 좌표
                    var layerLocalPoint = new Point(
                        localPoint.X - layerLeft,
                        localPoint.Y - layerTop);

                    var layerHit = FindHitInCanvas(layer, layerLocalPoint);
                    if (layerHit != null)
                        return layerHit;
                }
            }

            // 2차: 도형 hit 없으면 CanvasLayer 영역 자체를 반환 (레이어 이동/리사이즈 가능)
            for (int i = layered.Children.Count - 1; i >= 0; i--)
            {
                if (layered.Children[i] is CanvasLayer layer && layer.Visibility == Visibility.Visible)
                {
                    var layerBounds = GetChildBounds(layer);
                    if (!layerBounds.IsEmpty && layerBounds.Contains(localPoint))
                        return layer;
                }
            }

            return null;
        }

        /// <summary>
        /// 일반 Canvas 내부에서 HitTest를 수행합니다.
        /// </summary>
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

        /// <summary>
        /// 자식 요소의 바운딩 Rect를 반환합니다.
        /// </summary>
        private static Rect GetChildBounds(UIElement child)
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

        /// <summary>
        /// 요소의 부모 Canvas를 찾습니다.
        /// </summary>
        private static Canvas? FindParentCanvas(UIElement element)
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
        /// 요소가 특정 LayeredCanvas의 자식(CanvasLayer 포함)인지 확인합니다.
        /// </summary>
        private static bool IsChildOfLayered(UIElement? element, LayeredCanvas layered)
        {
            if (element == null) return false;
            DependencyObject? parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent == layered) return true;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return false;
        }

        /// <summary>
        /// 모든 자식 요소의 전체 바운딩 박스를 계산합니다.
        /// </summary>
        private Rect GetContentBounds()
        {
            var result = Rect.Empty;

            foreach (UIElement child in Children)
            {
                var bounds = GetChildBounds(child);
                if (!bounds.IsEmpty)
                {
                    result = result.IsEmpty ? bounds : Rect.Union(result, bounds);
                }
            }

            return result;
        }

        private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageCanvas canvas)
            {
                var newZoom = (double)e.NewValue;
                canvas._scaleTransform.ScaleX = newZoom;
                canvas._scaleTransform.ScaleY = newZoom;
            }
        }

        #endregion
    }
}
