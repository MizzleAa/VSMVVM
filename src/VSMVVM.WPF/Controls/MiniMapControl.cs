using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VSMVVM.WPF.Controls;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 타겟 <see cref="IZoomPanViewport"/>의 뷰포트를 축소 렌더하고,
    /// 자기 자신을 클릭/드래그해 타겟의 OffsetX/Y를 역방향으로 이동시키는 미니맵 컨트롤.
    /// </summary>
    public class MiniMapControl : Control
    {
        #region Fields

        private bool _isDragging;

        #endregion

        #region DependencyProperties

        public static readonly DependencyProperty TargetCanvasProperty =
            DependencyProperty.Register(
                nameof(TargetCanvas),
                typeof(IZoomPanViewport),
                typeof(MiniMapControl),
                new PropertyMetadata(null, OnTargetCanvasChanged));

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(
                nameof(ImageSource),
                typeof(ImageSource),
                typeof(MiniMapControl),
                new PropertyMetadata(null, (d, _) =>
                {
                    var c = (MiniMapControl)d;
                    c.InvalidateMeasure();
                    c.InvalidateVisual();
                }));

        public static readonly DependencyProperty MaskOverlayProperty =
            DependencyProperty.Register(
                nameof(MaskOverlay),
                typeof(MaskLayer),
                typeof(MiniMapControl),
                new PropertyMetadata(null, OnMaskOverlayChanged));

        private static void OnMaskOverlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (MiniMapControl)d;
            if (e.OldValue is MaskLayer oldM) oldM.DisplayChanged -= c.OnMaskOverlayDisplayChanged;
            if (e.NewValue is MaskLayer newM) newM.DisplayChanged += c.OnMaskOverlayDisplayChanged;
            c.InvalidateVisual();
        }

        private void OnMaskOverlayDisplayChanged(object? sender, EventArgs e) => InvalidateVisual();

        public static readonly DependencyProperty MaskOpacityProperty =
            DependencyProperty.Register(
                nameof(MaskOpacity),
                typeof(double),
                typeof(MiniMapControl),
                new PropertyMetadata(0.6, (d, _) => ((MiniMapControl)d).InvalidateVisual()));

        public static readonly DependencyProperty ViewportBrushProperty =
            DependencyProperty.Register(
                nameof(ViewportBrush),
                typeof(Brush),
                typeof(MiniMapControl),
                new PropertyMetadata(
                    Brushes.Transparent, // 배경을 가리지 않도록 기본 투명. 필요 시 XAML 에서 지정.
                    (d, _) => ((MiniMapControl)d).InvalidateVisual()));

        public static readonly DependencyProperty ViewportStrokeProperty =
            DependencyProperty.Register(
                nameof(ViewportStroke),
                typeof(Brush),
                typeof(MiniMapControl),
                new PropertyMetadata(
                    Brushes.Yellow,
                    (d, _) => ((MiniMapControl)d).InvalidateVisual()));

        public static readonly DependencyProperty MiniMapMaxWidthProperty =
            DependencyProperty.Register(
                nameof(MiniMapMaxWidth),
                typeof(double),
                typeof(MiniMapControl),
                new FrameworkPropertyMetadata(
                    220.0,
                    FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>미니맵이 추종할 대상 뷰포트.</summary>
        public IZoomPanViewport? TargetCanvas
        {
            get => (IZoomPanViewport?)GetValue(TargetCanvasProperty);
            set => SetValue(TargetCanvasProperty, value);
        }

        /// <summary>미니맵 배경에 그릴 이미지(보통 본 뷰의 원본 이미지).</summary>
        public ImageSource? ImageSource
        {
            get => (ImageSource?)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        /// <summary>배경 위에 얹을 마스크 레이어. 내부 DisplayImage 를 오버레이로 그린다.</summary>
        public MaskLayer? MaskOverlay
        {
            get => (MaskLayer?)GetValue(MaskOverlayProperty);
            set => SetValue(MaskOverlayProperty, value);
        }

        /// <summary>마스크 오버레이 불투명도 (0.0~1.0).</summary>
        public double MaskOpacity
        {
            get => (double)GetValue(MaskOpacityProperty);
            set => SetValue(MaskOpacityProperty, value);
        }

        /// <summary>뷰포트 사각형 채움 브러시.</summary>
        public Brush ViewportBrush
        {
            get => (Brush)GetValue(ViewportBrushProperty);
            set => SetValue(ViewportBrushProperty, value);
        }

        /// <summary>뷰포트 사각형 테두리.</summary>
        public Brush ViewportStroke
        {
            get => (Brush)GetValue(ViewportStrokeProperty);
            set => SetValue(ViewportStrokeProperty, value);
        }

        /// <summary>
        /// 미니맵 박스의 최대 너비. 높이는 콘텐츠(이미지) 종횡비에 따라 자동 결정된다.
        /// 이미지가 없을 때는 기본 종횡비(3:2) 기준으로 박스가 렌더된다.
        /// </summary>
        public double MiniMapMaxWidth
        {
            get => (double)GetValue(MiniMapMaxWidthProperty);
            set => SetValue(MiniMapMaxWidthProperty, value);
        }

        #endregion

        #region Constructor

        static MiniMapControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(MiniMapControl),
                new FrameworkPropertyMetadata(typeof(MiniMapControl)));
        }

        public MiniMapControl()
        {
            Focusable = false;
            Background = Brushes.Transparent;
            ClipToBounds = true;
            SnapsToDevicePixels = true;

            // 부모가 Stretch로 늘리지 않도록 Left/Top 고정. MeasureOverride가 종횡비에 맞는 Size를 반환.
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            VerticalAlignment = System.Windows.VerticalAlignment.Top;
        }

        #endregion

        #region Layout — 뷰포트 종횡비에 따라 자기 크기 결정

        /// <summary>
        /// 미니맵 박스 모양은 "뷰포트(Border) 모양의 축소판" — 타겟의 ViewportWidth/Height aspect를 따른다.
        /// 콘텐츠(이미지)는 이 박스 안에 letterbox로 들어간다.
        /// 뷰포트를 추종할 수 없는 초기 상태에서는 기본 3:2.
        /// </summary>
        protected override Size MeasureOverride(Size constraint)
        {
            double aspect = 3.0 / 2.0;
            var target = TargetCanvas;
            if (target != null && target.ViewportWidth > 0 && target.ViewportHeight > 0)
                aspect = target.ViewportWidth / target.ViewportHeight;

            double maxW = MiniMapMaxWidth > 0 ? MiniMapMaxWidth : 220.0;
            double width = maxW;
            if (!double.IsInfinity(constraint.Width))
                width = Math.Min(width, constraint.Width);

            double height = width / aspect;
            if (!double.IsInfinity(constraint.Height) && height > constraint.Height)
            {
                height = constraint.Height;
                width = height * aspect;
            }

            return new Size(width, height);
        }

        #endregion

        #region Target subscription

        private static void OnTargetCanvasChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MiniMapControl)d;
            if (e.OldValue is IZoomPanViewport oldVp)
            {
                oldVp.ViewportChanged -= control.OnTargetViewportChanged;
                oldVp.SizeChanged -= control.OnTargetSizeChanged;
            }
            if (e.NewValue is IZoomPanViewport newVp)
            {
                newVp.ViewportChanged += control.OnTargetViewportChanged;
                newVp.SizeChanged += control.OnTargetSizeChanged;
            }
            control.InvalidateVisual();
        }

        private void OnTargetViewportChanged(object? sender, EventArgs e)
        {
            // ContentWidth/Height가 FitToContent에서 설정되면 종횡비가 바뀌어 Measure도 갱신이 필요.
            InvalidateMeasure();
            InvalidateVisual();
        }

        private void OnTargetSizeChanged(object sender, SizeChangedEventArgs e) => InvalidateVisual();

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 배경 + 테두리
            dc.DrawRectangle(Background ?? Brushes.Transparent, null, new Rect(0, 0, w, h));
            if (BorderBrush != null && BorderThickness.Top > 0)
            {
                var borderPen = new Pen(BorderBrush, BorderThickness.Top);
                borderPen.Freeze();
                var half = BorderThickness.Top / 2.0;
                dc.DrawRectangle(null, borderPen, new Rect(half, half, w - BorderThickness.Top, h - BorderThickness.Top));
            }

            var contentSize = GetContentSize();
            if (contentSize.Width <= 0 || contentSize.Height <= 0) return;

            // 콘텐츠를 미니맵에 fit (letterbox)
            var scale = Math.Min(w / contentSize.Width, h / contentSize.Height);
            var drawW = contentSize.Width * scale;
            var drawH = contentSize.Height * scale;
            var offX = (w - drawW) / 2;
            var offY = (h - drawH) / 2;
            var contentRect = new Rect(offX, offY, drawW, drawH);

            if (ImageSource != null)
                dc.DrawImage(ImageSource, contentRect);
            else
                dc.DrawRectangle(Brushes.DimGray, null, contentRect);

            // 마스크 오버레이 (인스턴스 색상). 배경 이미지 위에 알파 블렌딩.
            var maskImg = MaskOverlay?.DisplayImage;
            if (maskImg != null)
            {
                dc.PushOpacity(MaskOpacity);
                dc.DrawImage(maskImg, contentRect);
                dc.Pop();
            }

            var viewport = GetViewportRectInContent();
            if (viewport.IsEmpty) return;

            var viewportOnMap = new Rect(
                offX + viewport.X * scale,
                offY + viewport.Y * scale,
                viewport.Width * scale,
                viewport.Height * scale);

            var pen = new Pen(ViewportStroke, 1);
            pen.Freeze();
            dc.DrawRectangle(ViewportBrush, pen, viewportOnMap);
        }

        /// <summary>
        /// 콘텐츠 크기(캔버스 좌표계). ContentWidth/Height DP 우선, 없으면 ImageSource의 픽셀 크기.
        /// </summary>
        private Size GetContentSize()
        {
            var target = TargetCanvas;
            if (target != null && target.ContentWidth > 0 && target.ContentHeight > 0)
                return new Size(target.ContentWidth, target.ContentHeight);

            if (ImageSource is BitmapSource bmp)
                return new Size(bmp.PixelWidth, bmp.PixelHeight);

            return new Size(0, 0);
        }

        /// <summary>
        /// 타겟의 현재 뷰포트를 캔버스 좌표계 기준 사각형으로 반환.
        /// 화면 (0,0)~(ActualWidth, ActualHeight)를 ScreenToCanvas로 역변환한 것과 같다.
        /// </summary>
        private Rect GetViewportRectInContent()
        {
            var target = TargetCanvas;
            if (target == null) return Rect.Empty;
            if (target.ViewportWidth <= 0 || target.ViewportHeight <= 0) return Rect.Empty;

            var topLeft = target.ScreenToCanvas(new Point(0, 0));
            var bottomRight = target.ScreenToCanvas(new Point(target.ViewportWidth, target.ViewportHeight));
            var rect = new Rect(topLeft, bottomRight);
            return rect;
        }

        #endregion

        #region Input — 클릭/드래그로 타겟 이동

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (TargetCanvas == null) return;

            _isDragging = true;
            CaptureMouse();
            MoveTargetToMapPoint(e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isDragging || TargetCanvas == null) return;
            MoveTargetToMapPoint(e.GetPosition(this));
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 미니맵 위 휠 → 메인 뷰포트 줌. clamp 와 뷰포트 중심 pivot 보정은 타겟의 SetZoom 이 담당한다.
        /// </summary>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            var target = TargetCanvas;
            if (target == null) return;

            var factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            var current = target.ZoomLevel > 0 ? target.ZoomLevel : 1.0;
            target.SetZoom(current * factor);
            e.Handled = true;
        }

        /// <summary>
        /// 미니맵 위의 클릭 지점을 뷰포트 중심이 되도록 타겟의 OffsetX/Y를 밀어넣는다.
        /// </summary>
        private void MoveTargetToMapPoint(Point mapPoint)
        {
            var target = TargetCanvas;
            if (target == null) return;

            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            var contentSize = GetContentSize();
            if (contentSize.Width <= 0 || contentSize.Height <= 0) return;

            var scale = Math.Min(w / contentSize.Width, h / contentSize.Height);
            var drawW = contentSize.Width * scale;
            var drawH = contentSize.Height * scale;
            var offX = (w - drawW) / 2;
            var offY = (h - drawH) / 2;

            // 미니맵 좌표 → 콘텐츠(캔버스) 좌표
            var canvasX = (mapPoint.X - offX) / scale;
            var canvasY = (mapPoint.Y - offY) / scale;

            // 그 지점이 뷰포트 중심에 오도록 Offset 계산.
            // screen = canvas * zoom + offset  →  offset = screen - canvas * zoom
            var zoom = target.ZoomLevel > 0 ? target.ZoomLevel : 1.0;
            var newOffsetX = target.ViewportWidth / 2 - canvasX * zoom;
            var newOffsetY = target.ViewportHeight / 2 - canvasY * zoom;

            target.SetOffset(newOffsetX, newOffsetY);
        }

        #endregion
    }
}
