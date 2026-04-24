using System;
using System.Windows;
using System.Windows.Media;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// Photopea 스타일 픽셀 격자. <see cref="TargetCanvas"/> 의 ZoomLevel 이
    /// <see cref="ActivationZoom"/> 이상일 때만 정수 픽셀 경계에 얇은 선을 그린다.
    /// 자체는 hit-test 비활성 — 순수 시각 오버레이.
    /// </summary>
    public class PixelGridLayer : FrameworkElement
    {
        public static readonly DependencyProperty TargetCanvasProperty =
            DependencyProperty.Register(nameof(TargetCanvas), typeof(IZoomPanViewport), typeof(PixelGridLayer),
                new PropertyMetadata(null, OnTargetCanvasChanged));

        public static readonly DependencyProperty ActivationZoomProperty =
            DependencyProperty.Register(nameof(ActivationZoom), typeof(double), typeof(PixelGridLayer),
                new PropertyMetadata(6.0, (d, _) => ((PixelGridLayer)d).InvalidateVisual()));

        public static readonly DependencyProperty GridBrushProperty =
            DependencyProperty.Register(nameof(GridBrush), typeof(Brush), typeof(PixelGridLayer),
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
                    (d, _) => ((PixelGridLayer)d).InvalidateVisual()));

        public static readonly DependencyProperty LineThicknessPixelsProperty =
            DependencyProperty.Register(nameof(LineThicknessPixels), typeof(double), typeof(PixelGridLayer),
                new PropertyMetadata(1.0, (d, _) => ((PixelGridLayer)d).InvalidateVisual()));

        public IZoomPanViewport? TargetCanvas
        {
            get => (IZoomPanViewport?)GetValue(TargetCanvasProperty);
            set => SetValue(TargetCanvasProperty, value);
        }

        public double ActivationZoom
        {
            get => (double)GetValue(ActivationZoomProperty);
            set => SetValue(ActivationZoomProperty, value);
        }

        public Brush GridBrush
        {
            get => (Brush)GetValue(GridBrushProperty);
            set => SetValue(GridBrushProperty, value);
        }

        public double LineThicknessPixels
        {
            get => (double)GetValue(LineThicknessPixelsProperty);
            set => SetValue(LineThicknessPixelsProperty, value);
        }

        public PixelGridLayer()
        {
            IsHitTestVisible = false;
            SnapsToDevicePixels = true;
        }

        private static void OnTargetCanvasChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (PixelGridLayer)d;
            if (e.OldValue is IZoomPanViewport oldVp)
            {
                oldVp.ViewportChanged -= self.OnViewportChanged;
                oldVp.SizeChanged -= self.OnViewportSizeChanged;
            }
            if (e.NewValue is IZoomPanViewport newVp)
            {
                newVp.ViewportChanged += self.OnViewportChanged;
                newVp.SizeChanged += self.OnViewportSizeChanged;
            }
            self.InvalidateVisual();
        }

        private void OnViewportChanged(object? sender, EventArgs e) => InvalidateVisual();
        private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e) => InvalidateVisual();

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var vp = TargetCanvas;
            if (vp == null) return;

            double zoom = vp.ZoomLevel;
            if (zoom < ActivationZoom) return;

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 보이는 pre-transform 픽셀 범위 계산.
            // 뷰포트 (0,0) → canvas 좌표 변환 후 자기 Left/Top 은 0 이라 가정 (샘플 배치와 동일).
            var topLeft = vp.ScreenToCanvas(new Point(0, 0));
            var bottomRight = vp.ScreenToCanvas(new Point(vp.ViewportWidth, vp.ViewportHeight));

            int x0 = Math.Max(0, (int)Math.Floor(topLeft.X));
            int y0 = Math.Max(0, (int)Math.Floor(topLeft.Y));
            int x1 = Math.Min((int)Math.Ceiling(w), (int)Math.Ceiling(bottomRight.X));
            int y1 = Math.Min((int)Math.Ceiling(h), (int)Math.Ceiling(bottomRight.Y));
            if (x1 <= x0 || y1 <= y0) return;

            // pen 두께는 자식 pre-transform 좌표계 기준. 화면 1 DIU 를 유지하려면 1/zoom.
            double penThickness = LineThicknessPixels / zoom;
            var pen = new Pen(GridBrush, penThickness);
            pen.Freeze();

            double yTop = y0;
            double yBot = y1;
            for (int x = x0; x <= x1; x++)
            {
                dc.DrawLine(pen, new Point(x, yTop), new Point(x, yBot));
            }
            double xLeft = x0;
            double xRight = x1;
            for (int y = y0; y <= y1; y++)
            {
                dc.DrawLine(pen, new Point(xLeft, y), new Point(xRight, y));
            }
        }
    }
}
