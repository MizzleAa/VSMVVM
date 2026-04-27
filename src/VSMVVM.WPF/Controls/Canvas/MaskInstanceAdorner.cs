using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using VSMVVM.WPF.Imaging;
using MediaImageSource = System.Windows.Media.ImageSource;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 선택된 <see cref="MaskInstance"/> 의 BBox 위에 8방향 리사이즈 핸들을 그리는 Adorner.
    /// 드래그 시 임시 프리뷰 Rect 만 갱신하고, MouseUp 에서 <see cref="MaskLayer.ResampleInstance"/> 를 1회 호출.
    /// </summary>
    public sealed class MaskInstanceAdorner : Adorner
    {
        private readonly MaskLayer _mask;
        private MaskInstance _instance;
        private Rect _previewBBox;
        private bool _hasPreview;
        private HandlePos _activeHandle = HandlePos.None;
        private Point _dragStart;
        private Rect _dragStartBBox;
        private MediaImageSource? _silhouette; // 인스턴스 픽셀 실루엣 캐시 (드래그 프리뷰용)

        private const double HandleSize = 8.0;

        private enum HandlePos { None, N, S, E, W, NE, NW, SE, SW, Inside }

        /// <summary>현재 MaskLayer 의 RenderTransform scale(=zoom). 핸들/펜 두께를 화면 기준 DIU 로 유지하기 위해 1/scale 보정에 사용.</summary>
        private double GetZoom()
        {
            if (_mask.RenderTransform is MatrixTransform mt)
            {
                double s = mt.Matrix.M11;
                return s > 0.0001 ? s : 1.0;
            }
            return 1.0;
        }

        public MaskInstanceAdorner(MaskLayer mask, MaskInstance instance) : base(mask)
        {
            _mask = mask;
            _instance = instance;
            _previewBBox = ClampBBox(instance.BoundingBox);
            IsHitTestVisible = true;
            // 실루엣 프리뷰는 픽셀 단위 nearest-neighbor 로 확대되도록.
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            // _silhouette 는 드래그 시작 시점에만 lazy 로 만든다 (큰 마스크에서 단순 선택 lag 회피).
            instance.PropertyChanged += OnInstancePropertyChanged;
        }

        public MaskInstance Instance
        {
            get => _instance;
            set
            {
                if (_instance != null) _instance.PropertyChanged -= OnInstancePropertyChanged;
                _instance = value;
                _previewBBox = ClampBBox(value.BoundingBox);
                _silhouette = null; // 새 인스턴스는 드래그 시작 시점에 다시 만듦.
                if (_instance != null) _instance.PropertyChanged += OnInstancePropertyChanged;
                InvalidateVisual();
            }
        }

        private void OnInstancePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MaskInstance.BoundingBox) && !_hasPreview)
            {
                _previewBBox = ClampBBox(_instance.BoundingBox);
                InvalidateVisual();
            }
        }

        /// <summary>BBox 를 이미지(마스크) 경계 안으로 Intersect.</summary>
        private Rect ClampBBox(Rect r)
        {
            if (r.IsEmpty) return r;
            var bounds = new Rect(0, 0, _mask.MaskWidth, _mask.MaskHeight);
            r.Intersect(bounds);
            return r.IsEmpty ? Rect.Empty : r;
        }

        /// <summary>Pixel BBox → MaskLayer 로컬(DIU) 좌표 변환. Empty/비정상 입력은 Rect.Empty 반환.</summary>
        private Rect PixelToLocal(Rect px)
        {
            // Rect.Empty 는 Width/Height 가 double.NegativeInfinity → 생성자 throw 방지.
            if (px.IsEmpty || px.Width < 0 || px.Height < 0) return Rect.Empty;
            if (_mask.MaskWidth == 0 || _mask.MaskHeight == 0) return Rect.Empty;
            var displayW = _mask.ActualWidth > 0 ? _mask.ActualWidth : _mask.MaskWidth;
            var displayH = _mask.ActualHeight > 0 ? _mask.ActualHeight : _mask.MaskHeight;
            double sx = displayW / _mask.MaskWidth;
            double sy = displayH / _mask.MaskHeight;
            return new Rect(px.X * sx, px.Y * sy, px.Width * sx, px.Height * sy);
        }

        private Point LocalToPixel(Point local)
        {
            var displayW = _mask.ActualWidth > 0 ? _mask.ActualWidth : _mask.MaskWidth;
            var displayH = _mask.ActualHeight > 0 ? _mask.ActualHeight : _mask.MaskHeight;
            if (displayW == 0 || displayH == 0) return local;
            return new Point(local.X * _mask.MaskWidth / displayW, local.Y * _mask.MaskHeight / displayH);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (_instance == null) return;

            // AdornerLayer 는 Window 전체 영역에 그려 부모 ClipToBounds 무시 → 명시적 clip.
            // AdornedElement(=_mask) 의 로컬 bounds 로 강제 clip.
            var clipBounds = new Rect(0, 0,
                _mask.ActualWidth > 0 ? _mask.ActualWidth : _mask.MaskWidth,
                _mask.ActualHeight > 0 ? _mask.ActualHeight : _mask.MaskHeight);
            dc.PushClip(new RectangleGeometry(clipBounds));
            try
            {
                var local = PixelToLocal(_previewBBox);
                if (local.IsEmpty) return;

                // 실제 인스턴스 픽셀 실루엣을 _previewBBox 에 맞춰 그림.
                if (_silhouette != null)
                {
                    double alpha = _activeHandle != HandlePos.None ? 0.7 : 0.45;
                    dc.PushOpacity(alpha);
                    dc.DrawImage(_silhouette, local);
                    dc.Pop();
                }

                // Zoom 보정: transform 상속으로 화면에서 zoom 배로 커지는 것을 1/zoom 으로 상쇄.
                double z = GetZoom();
                double h = HandleSize / z;

                // 점선 테두리.
                var pen = new Pen(Brushes.Yellow, 1.5 / z) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
                pen.Freeze();
                dc.DrawRectangle(null, pen, local);

                // 8 handles
                var handleBorder = new Pen(Brushes.Black, 1.0 / z);
                handleBorder.Freeze();
                foreach (var (pos, pt) in EnumerateHandlePoints(local))
                {
                    var rect = new Rect(pt.X - h / 2, pt.Y - h / 2, h, h);
                    dc.DrawRectangle(Brushes.White, handleBorder, rect);
                }
            }
            finally
            {
                dc.Pop();
            }
        }

        private static System.Collections.Generic.IEnumerable<(HandlePos pos, Point pt)> EnumerateHandlePoints(Rect r)
        {
            yield return (HandlePos.NW, new Point(r.Left, r.Top));
            yield return (HandlePos.N, new Point(r.Left + r.Width / 2, r.Top));
            yield return (HandlePos.NE, new Point(r.Right, r.Top));
            yield return (HandlePos.E, new Point(r.Right, r.Top + r.Height / 2));
            yield return (HandlePos.SE, new Point(r.Right, r.Bottom));
            yield return (HandlePos.S, new Point(r.Left + r.Width / 2, r.Bottom));
            yield return (HandlePos.SW, new Point(r.Left, r.Bottom));
            yield return (HandlePos.W, new Point(r.Left, r.Top + r.Height / 2));
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_instance == null) return;

            // 더블클릭 → vertex 편집 모드로 전환. PolygonPoints 가 없으면 마스크 픽셀에서 외곽을 lazy 추출.
            if (e.ClickCount == 2)
            {
                _mask.EnsurePolygonPoints(_instance.Id);
                if (_instance.PolygonPoints != null && _instance.PolygonPoints.Count >= 3)
                {
                    _mask.IsVertexEditMode = true;
                    e.Handled = true;
                    return;
                }
            }

            var pos = e.GetPosition(this);
            _activeHandle = HitTestHandle(pos);
            if (_activeHandle == HandlePos.None) return;

            _dragStart = pos;
            _dragStartBBox = _previewBBox;
            _silhouette = _mask.GetInstanceSilhouette(_instance.Id); // 드래그 시작 시점 상태 캡처
            _hasPreview = true;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_activeHandle == HandlePos.None) return;
            var cur = e.GetPosition(this);
            var curPx = LocalToPixel(cur);
            var startPx = LocalToPixel(_dragStart);
            double dx = curPx.X - startPx.X;
            double dy = curPx.Y - startPx.Y;

            var r = _dragStartBBox;

            if (_activeHandle == HandlePos.Inside)
            {
                // 크기 유지하면서 translate.
                double tx = r.X + dx, ty = r.Y + dy;
                // 이미지 경계 clamp.
                tx = Math.Max(0, Math.Min(tx, _mask.MaskWidth - r.Width));
                ty = Math.Max(0, Math.Min(ty, _mask.MaskHeight - r.Height));
                _previewBBox = new Rect(tx, ty, r.Width, r.Height);
                InvalidateVisual();
                return;
            }

            double left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom;
            switch (_activeHandle)
            {
                case HandlePos.N: top += dy; break;
                case HandlePos.S: bottom += dy; break;
                case HandlePos.E: right += dx; break;
                case HandlePos.W: left += dx; break;
                case HandlePos.NE: top += dy; right += dx; break;
                case HandlePos.NW: top += dy; left += dx; break;
                case HandlePos.SE: bottom += dy; right += dx; break;
                case HandlePos.SW: bottom += dy; left += dx; break;
            }
            if (right <= left + 1) right = left + 1;
            if (bottom <= top + 1) bottom = top + 1;
            // 이미지 경계 clamp.
            left = Math.Max(0, left);
            top = Math.Max(0, top);
            right = Math.Min(_mask.MaskWidth, right);
            bottom = Math.Min(_mask.MaskHeight, bottom);
            if (right <= left + 1) right = left + 1;
            if (bottom <= top + 1) bottom = top + 1;
            _previewBBox = new Rect(left, top, right - left, bottom - top);
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_activeHandle == HandlePos.None) return;
            _activeHandle = HandlePos.None;
            ReleaseMouseCapture();
            CommitResize();
            _hasPreview = false;
        }

        private void CommitResize()
        {
            if (_instance == null) return;
            RaiseCommitRequested(_instance.Id, _previewBBox);
        }

        /// <summary>
        /// 실제 Resample + Undo 스냅샷 푸시는 외부(예: LayeredCanvas 나 MaskBehavior) 가 담당하도록 이벤트로 넘긴다.
        /// </summary>
        public event EventHandler<MaskInstanceResizeEventArgs>? CommitRequested;

        private void RaiseCommitRequested(uint id, Rect newBBox)
            => CommitRequested?.Invoke(this, new MaskInstanceResizeEventArgs(id, newBBox));

        private HandlePos HitTestHandle(Point p)
        {
            var local = PixelToLocal(_previewBBox);
            if (local.IsEmpty) return HandlePos.None;
            double h = HandleSize / GetZoom();
            foreach (var (pos, pt) in EnumerateHandlePoints(local))
            {
                if (Math.Abs(p.X - pt.X) <= h && Math.Abs(p.Y - pt.Y) <= h)
                    return pos;
            }
            // 핸들이 아니면 BBox 내부 hit → translate 모드.
            if (local.Contains(p)) return HandlePos.Inside;
            return HandlePos.None;
        }

        /// <summary>
        /// 마우스 휠은 Adorner 가 가로채지 않고 부모 LayeredCanvas 의 zoom 에 도달하도록 forward.
        /// AdornerLayer 는 LayeredCanvas 의 visual tree 가 아니라 AdornerDecorator 자식이라
        /// 자연스러운 bubbling 으로는 LayeredCanvas.OnMouseWheel 까지 가지 않는다.
        /// </summary>
        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);
            if (e.Handled) return;
            e.Handled = true;
            var forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = _mask
            };
            _mask.RaiseEvent(forwarded);
        }

        /// <summary>
        /// BBox 영역(+ 핸들 주변 여유) 전체를 hit-test 대상으로 선언.
        /// 실루엣 이미지의 투명 픽셀에도 이벤트를 받기 위해 필요.
        /// </summary>
        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
        {
            var p = hitTestParameters.HitPoint;
            var local = PixelToLocal(_previewBBox);
            if (local.IsEmpty) return null;
            double h = HandleSize / GetZoom();
            var expanded = new Rect(
                local.X - h,
                local.Y - h,
                local.Width + h * 2,
                local.Height + h * 2);
            if (expanded.Contains(p))
                return new PointHitTestResult(this, p);
            return null;
        }
    }

    public sealed class MaskInstanceResizeEventArgs : EventArgs
    {
        public MaskInstanceResizeEventArgs(uint id, Rect newBBox)
        {
            InstanceId = id;
            NewBoundingBox = newBBox;
        }

        public uint InstanceId { get; }
        public Rect NewBoundingBox { get; }
    }
}
