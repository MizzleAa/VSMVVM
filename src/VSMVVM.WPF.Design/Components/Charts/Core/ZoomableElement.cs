using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    /// <summary>
    /// 줌/팬을 지원하는 FrameworkElement 베이스. content 영역만 transform되고
    /// 컬러바·축 라벨 등은 자식이 PushTransform 밖에서 그리도록 한다.
    /// </summary>
    public abstract class ZoomableElement : FrameworkElement, IFittable
    {
        public static readonly DependencyProperty MinZoomProperty =
            DependencyProperty.Register(nameof(MinZoom), typeof(double), typeof(ZoomableElement),
                new PropertyMetadata(0.5));

        public static readonly DependencyProperty MaxZoomProperty =
            DependencyProperty.Register(nameof(MaxZoom), typeof(double), typeof(ZoomableElement),
                new PropertyMetadata(20.0));

        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(ZoomableElement),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsZoomEnabledProperty =
            DependencyProperty.Register(nameof(IsZoomEnabled), typeof(bool), typeof(ZoomableElement),
                new PropertyMetadata(true));

        public static readonly DependencyProperty IsPanEnabledProperty =
            DependencyProperty.Register(nameof(IsPanEnabled), typeof(bool), typeof(ZoomableElement),
                new PropertyMetadata(true));

        public double MinZoom { get => (double)GetValue(MinZoomProperty); set => SetValue(MinZoomProperty, value); }
        public double MaxZoom { get => (double)GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }
        public double ZoomLevel { get => (double)GetValue(ZoomLevelProperty); set => SetValue(ZoomLevelProperty, value); }
        public bool IsZoomEnabled { get => (bool)GetValue(IsZoomEnabledProperty); set => SetValue(IsZoomEnabledProperty, value); }
        public bool IsPanEnabled { get => (bool)GetValue(IsPanEnabledProperty); set => SetValue(IsPanEnabledProperty, value); }

        protected double Scale = 1.0;
        protected double TranslateX;
        protected double TranslateY;

        private bool _isPanning;
        private Point _panStart;
        private double _panStartTx, _panStartTy;

        protected ZoomableElement()
        {
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
        }

        /// <summary>자식 OnRender에서 zoom/pan이 적용된 transform을 사용하려면 이 객체로 PushTransform 한다.</summary>
        protected MatrixTransform CurrentTransform
        {
            get
            {
                var m = new Matrix(Scale, 0, 0, Scale, TranslateX, TranslateY);
                var t = new MatrixTransform(m);
                t.Freeze();
                return t;
            }
        }

        /// <summary>Screen(view) 좌표를 transform 적용 전 content 좌표로 역변환.</summary>
        public Point ScreenToContent(Point screen)
        {
            if (Scale == 0) return screen;
            return new Point((screen.X - TranslateX) / Scale, (screen.Y - TranslateY) / Scale);
        }

        public virtual void FitToContent()
        {
            Scale = 1.0;
            TranslateX = 0;
            TranslateY = 0;
            ZoomLevel = 1.0;
            InvalidateVisual();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!IsZoomEnabled || e.Handled) return;

            var pivot = e.GetPosition(this);
            var k = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            var newScale = Scale * k;
            newScale = Math.Max(MinZoom, Math.Min(MaxZoom, newScale));
            var actualK = newScale / Scale;
            if (Math.Abs(actualK - 1) < 1e-6) { e.Handled = true; return; }

            // pivot 위치 고정: t_new = (t - p) * k + p
            TranslateX = (TranslateX - pivot.X) * actualK + pivot.X;
            TranslateY = (TranslateY - pivot.Y) * actualK + pivot.Y;
            Scale = newScale;
            ZoomLevel = newScale;

            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (!IsPanEnabled || e.Handled) return;
            Focus();
            _isPanning = true;
            _panStart = e.GetPosition(this);
            _panStartTx = TranslateX;
            _panStartTy = TranslateY;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isPanning)
            {
                var pos = e.GetPosition(this);
                TranslateX = _panStartTx + (pos.X - _panStart.X);
                TranslateY = _panStartTy + (pos.Y - _panStart.Y);
                InvalidateVisual();
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

        public bool IsPanning => _isPanning;
    }
}
