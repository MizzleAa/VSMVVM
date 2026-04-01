using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VSMVVM.WPF.Controls.Tools;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 배경 뷰포트. 마우스 커서 기준 피벗 줌 + 드래그 팬을 지원합니다.
    /// LayeredCanvas의 부모로 사용됩니다.
    /// </summary>
    public class BackgroundCanvas : Canvas
    {
        #region Fields

        private readonly ScaleTransform _scaleTransform = new ScaleTransform(1.0, 1.0);
        private readonly TranslateTransform _translateTransform = new TranslateTransform();
        private readonly TransformGroup _transformGroup = new TransformGroup();

        private bool _isPanning;
        private Point _panStart;

        #endregion

        #region DependencyProperties

        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register(
                nameof(ZoomLevel),
                typeof(double),
                typeof(BackgroundCanvas),
                new PropertyMetadata(1.0, OnZoomLevelChanged));

        public static readonly DependencyProperty MinZoomProperty =
            DependencyProperty.Register(
                nameof(MinZoom),
                typeof(double),
                typeof(BackgroundCanvas),
                new PropertyMetadata(0.1));

        public static readonly DependencyProperty MaxZoomProperty =
            DependencyProperty.Register(
                nameof(MaxZoom),
                typeof(double),
                typeof(BackgroundCanvas),
                new PropertyMetadata(20.0));

        public static readonly DependencyProperty IsPanLockedProperty =
            DependencyProperty.Register(
                nameof(IsPanLocked),
                typeof(bool),
                typeof(BackgroundCanvas),
                new PropertyMetadata(false));

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

        /// <summary>
        /// true이면 드래그 팬이 비활성화됩니다.
        /// </summary>
        public bool IsPanLocked
        {
            get => (bool)GetValue(IsPanLockedProperty);
            set => SetValue(IsPanLockedProperty, value);
        }

        /// <summary>
        /// 현재 활성 도구. LayeredCanvas와 동일한 Tool 인스턴스를 바인딩.
        /// </summary>
        public static readonly DependencyProperty CurrentToolProperty =
            DependencyProperty.Register(
                nameof(CurrentTool),
                typeof(ICanvasTool),
                typeof(BackgroundCanvas),
                new PropertyMetadata(null, OnCurrentToolChanged));

        public ICanvasTool? CurrentTool
        {
            get => (ICanvasTool?)GetValue(CurrentToolProperty);
            set => SetValue(CurrentToolProperty, value);
        }

        private static void OnCurrentToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BackgroundCanvas canvas && e.NewValue is ICanvasTool tool)
            {
                canvas.Cursor = tool.ToolCursor;
            }
        }

        #endregion

        #region Constructor

        public BackgroundCanvas()
        {
            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);
            RenderTransform = _transformGroup;
            Background = Brushes.Transparent; // hit-test 활성화 (팬 드래그용)
            SizeChanged += (_, __) => UpdateLayeredCanvasSize();
        }

        #endregion

        #region Overrides

        /// <summary>
        /// 마우스 휠 줌. 마우스 커서 위치 기준 피벗 줌.
        /// </summary>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            var position = e.GetPosition(this.Parent as IInputElement);
            var zoomFactor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            var newZoom = ZoomLevel * zoomFactor;
            newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));

            // 마우스 위치 기준 피벗 줌
            var canvasX = (position.X - _translateTransform.X) / _scaleTransform.ScaleX;
            var canvasY = (position.Y - _translateTransform.Y) / _scaleTransform.ScaleY;

            _scaleTransform.ScaleX = newZoom;
            _scaleTransform.ScaleY = newZoom;

            _translateTransform.X = position.X - canvasX * newZoom;
            _translateTransform.Y = position.Y - canvasY * newZoom;

            ZoomLevel = newZoom;

            foreach (UIElement child in Children)
            {
                if (child is LayeredCanvas lc)
                    lc.InvalidateAdorner();
            }

            e.Handled = true;
        }

        /// <summary>
        /// 빈 영역 좌클릭 → 팬 드래그 시작.
        /// LayeredCanvas가 도형 선택 시 e.Handled=true 설정하므로, 빈 영역만 여기로 도달.
        /// </summary>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.Handled || IsPanLocked) return;

            var tool = CurrentTool;
            var isDrawingMode = tool != null && tool.Mode != CanvasToolMode.Select;

            // 그리기 모드에서는 Ctrl+드래그만 Pan 허용
            if (isDrawingMode)
            {
                bool isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                if (!isCtrl) return;
            }

            _isPanning = true;
            _panStart = e.GetPosition(this.Parent as IInputElement);
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isPanning)
            {
                var pos = e.GetPosition(this.Parent as IInputElement);
                _translateTransform.X += pos.X - _panStart.X;
                _translateTransform.Y += pos.Y - _panStart.Y;
                _panStart = pos;

                foreach (UIElement child in Children)
                {
                    if (child is LayeredCanvas lc)
                        lc.InvalidateAdorner();
                }

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
            }
        }

        #endregion

        #region Public Methods

        public void ResetView()
        {
            _scaleTransform.ScaleX = 1.0;
            _scaleTransform.ScaleY = 1.0;
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
            ZoomLevel = 1.0;
            UpdateLayeredCanvasSize();
        }

        /// <summary>
        /// 부모 뷰포트 기준으로 줌을 맞춥니다.
        /// LayeredCanvas가 뷰포트를 정확히 채우도록 zoom=1, translate=0으로 리셋.
        /// </summary>
        public void FitToContent()
        {
            var parent = Parent as FrameworkElement;
            if (parent == null || parent.ActualWidth <= 0 || parent.ActualHeight <= 0) return;

            // 부모 뷰포트 기준: zoom=1에서 LayeredCanvas가 뷰포트를 정확히 채움
            _scaleTransform.ScaleX = 1.0;
            _scaleTransform.ScaleY = 1.0;
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
            ZoomLevel = 1.0;
            UpdateLayeredCanvasSize();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 자식 LayeredCanvas의 크기를 뷰포트에 맞게 갱신합니다.
        /// 위치(SetLeft/SetTop)는 변경하지 않음 — 줌 시 이동하면 자식 도형이 드리프트됨.
        /// BackgroundCanvas의 RenderTransform이 줌/팬 시각 효과를 전담합니다.
        /// </summary>
        private void UpdateLayeredCanvasSize()
        {
            var parent = Parent as FrameworkElement;
            if (parent == null || parent.ActualWidth <= 0 || parent.ActualHeight <= 0) return;

            foreach (UIElement child in Children)
            {
                if (child is LayeredCanvas lc)
                {
                    lc.Width = parent.ActualWidth;
                    lc.Height = parent.ActualHeight;
                }
            }
        }

        private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BackgroundCanvas canvas)
            {
                var newZoom = (double)e.NewValue;
                canvas._scaleTransform.ScaleX = newZoom;
                canvas._scaleTransform.ScaleY = newZoom;
            }
        }

        #endregion
    }
}
