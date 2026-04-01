using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 순수 Zoom/Pan 뷰포트 컨트롤.
    /// 마우스 휠 줌, 드래그 팬만 지원합니다.
    /// 선택/드래그는 자식 LayeredCanvas에서 처리합니다.
    /// </summary>
    public class ImageCanvas : Canvas
    {
        #region Fields

        private readonly ScaleTransform _scaleTransform = new ScaleTransform(1.0, 1.0);
        private readonly TranslateTransform _translateTransform = new TranslateTransform();
        private readonly TransformGroup _transformGroup = new TransformGroup();
        private Point _lastMousePosition;
        private bool _isPanning;

        #endregion

        #region DependencyProperties

        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register(
                nameof(ZoomLevel),
                typeof(double),
                typeof(ImageCanvas),
                new PropertyMetadata(1.0, OnZoomLevelChanged));

        public static readonly DependencyProperty MinZoomProperty =
            DependencyProperty.Register(
                nameof(MinZoom),
                typeof(double),
                typeof(ImageCanvas),
                new PropertyMetadata(0.1));

        public static readonly DependencyProperty MaxZoomProperty =
            DependencyProperty.Register(
                nameof(MaxZoom),
                typeof(double),
                typeof(ImageCanvas),
                new PropertyMetadata(20.0));

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

        #endregion

        #region Constructor

        public ImageCanvas()
        {
            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);
            RenderTransform = _transformGroup;
            ClipToBounds = true;
            SizeChanged += (_, __) => UpdateLayeredCanvasSize();
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
            UpdateLayeredCanvasSize();

            // 자식 LayeredCanvas의 adorner 갱신
            foreach (UIElement child in Children)
            {
                if (child is LayeredCanvas lc)
                    lc.InvalidateAdorner();
            }

            e.Handled = true;
        }

        /// <summary>
        /// 마우스 다운: 빈 공간 클릭 시 팬 시작.
        /// LayeredCanvas 위 클릭은 LayeredCanvas가 자체 처리.
        /// </summary>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            // 자식 LayeredCanvas가 이미 드래그를 시작했으면 panning 하지 않음
            if (e.Handled) return;

            // LayeredCanvas에서 요소가 선택되었으면 panning 차단
            foreach (UIElement child in Children)
            {
                if (child is LayeredCanvas lc && lc.IsDragging)
                    return;
            }

            _isPanning = true;
            _lastMousePosition = e.GetPosition(Parent as IInputElement);
            CaptureMouse();
        }

        /// <summary>
        /// 마우스 이동: 팬.
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isPanning)
            {
                var currentPosition = e.GetPosition(Parent as IInputElement);
                var delta = currentPosition - _lastMousePosition;

                _translateTransform.X += delta.X;
                _translateTransform.Y += delta.Y;

                _lastMousePosition = currentPosition;
                UpdateLayeredCanvasSize();
            }
        }

        /// <summary>
        /// 마우스 업: 팬 종료.
        /// </summary>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (_isPanning)
            {
                _isPanning = false;
                ReleaseMouseCapture();
            }
        }

        #endregion

        #region Public Methods

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
            var zoom = Math.Min(scaleX, scaleY) * 0.9;

            zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));

            _scaleTransform.ScaleX = zoom;
            _scaleTransform.ScaleY = zoom;

            var contentCenterX = bounds.X + bounds.Width / 2;
            var contentCenterY = bounds.Y + bounds.Height / 2;

            _translateTransform.X = viewportWidth / 2 - contentCenterX * zoom;
            _translateTransform.Y = viewportHeight / 2 - contentCenterY * zoom;

            ZoomLevel = zoom;
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
        }

        #endregion

        #region Private Methods

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

        /// <summary>
        /// 자식 LayeredCanvas의 위치/크기를 뷰포트에 맞게 갱신합니다.
        /// 줌/팬 상태에 관계없이 LayeredCanvas가 전체 뷰포트를 채웁니다.
        /// </summary>
        private void UpdateLayeredCanvasSize()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            var zoom = ZoomLevel > 0 ? ZoomLevel : 1.0;

            foreach (UIElement child in Children)
            {
                if (child is LayeredCanvas lc)
                {
                    // 뷰포트를 정확히 채우려면:
                    // 캔버스 좌표에서 뷰포트 좌상단 = (-translate / zoom)
                    // 캔버스 좌표에서 뷰포트 크기 = (viewport / zoom)
                    SetLeft(lc, -_translateTransform.X / zoom);
                    SetTop(lc, -_translateTransform.Y / zoom);
                    lc.Width = ActualWidth / zoom;
                    lc.Height = ActualHeight / zoom;
                }
            }
        }

        #endregion
    }
}
