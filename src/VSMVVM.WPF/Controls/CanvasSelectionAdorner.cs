using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 선택된 캔버스 자식 요소에 부착되는 Adorner.
    /// 선택 테두리와 8방향 리사이즈 핸들을 표시합니다.
    /// LayeredCanvas 리사이즈 시 원본 스냅샷 기반 비례 스케일링.
    /// </summary>
    public class CanvasSelectionAdorner : Adorner
    {
        #region Fields

        private const double HandleSize = 8.0;

        private readonly Pen _borderPen;
        private readonly Brush _handleFill;
        private readonly Pen _handleStroke;

        private HandlePosition _activeHandle = HandlePosition.None;
        private Point _dragStart;
        private Rect _originalRect;
        private bool _isResizing;

        // 원본 스냅샷: LayeredCanvas 리사이즈 시 자식의 초기 상태 보존
        private Dictionary<UIElement, Rect>? _originalChildStates;

        #endregion

        #region Constructor

        public CanvasSelectionAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            var accentBrush = TryFindBrush("AccentPrimary") ?? Brushes.DodgerBlue;
            var bgBrush = TryFindBrush("BgPrimary") ?? Brushes.White;

            _borderPen = new Pen(accentBrush, 1.5) { DashStyle = DashStyles.Dash };
            _handleFill = bgBrush;
            _handleStroke = new Pen(accentBrush, 1.5);

            IsHitTestVisible = true;
        }

        #endregion

        #region Overrides

        protected override void OnRender(DrawingContext drawingContext)
        {
            var element = AdornedElement as FrameworkElement;
            if (element == null) return;

            var width = element.ActualWidth;
            var height = element.ActualHeight;

            if (width <= 0 || height <= 0) return;

            // 뷰포트 클리핑: adorner가 BackgroundCanvas 영역 밖으로 렌더링되지 않도록
            bool clipped = PushViewportClip(drawingContext);

            var zoomCompensation = GetZoomCompensation();
            var handleSize = HandleSize * zoomCompensation;
            var halfHandle = handleSize / 2.0;

            var borderPen = new Pen(_borderPen.Brush, _borderPen.Thickness * zoomCompensation)
            {
                DashStyle = DashStyles.Dash
            };
            drawingContext.DrawRectangle(null, borderPen, new Rect(0, 0, width, height));

            var handleStroke = new Pen(_handleStroke.Brush, _handleStroke.Thickness * zoomCompensation);

            DrawHandle(drawingContext, 0, 0, handleSize, halfHandle, handleStroke);
            DrawHandle(drawingContext, width / 2, 0, handleSize, halfHandle, handleStroke);
            DrawHandle(drawingContext, width, 0, handleSize, halfHandle, handleStroke);
            DrawHandle(drawingContext, width, height / 2, handleSize, halfHandle, handleStroke);
            DrawHandle(drawingContext, width, height, handleSize, halfHandle, handleStroke);
            DrawHandle(drawingContext, width / 2, height, handleSize, halfHandle, handleStroke);
            DrawHandle(drawingContext, 0, height, handleSize, halfHandle, handleStroke);
            DrawHandle(drawingContext, 0, height / 2, handleSize, halfHandle, handleStroke);

            if (clipped) drawingContext.Pop();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            var element = AdornedElement as FrameworkElement;
            if (element == null) return;

            var pos = e.GetPosition(this);
            _activeHandle = HitTestHandle(pos, element.ActualWidth, element.ActualHeight);

            if (_activeHandle != HandlePosition.None)
            {
                _isResizing = true;
                _dragStart = e.GetPosition(FindCanvas());
                _originalRect = new Rect(
                    Canvas.GetLeft(element),
                    Canvas.GetTop(element),
                    element.ActualWidth,
                    element.ActualHeight);

                if (double.IsNaN(_originalRect.X)) _originalRect.X = 0;
                if (double.IsNaN(_originalRect.Y)) _originalRect.Y = 0;

                // LayeredCanvas일 경우 자식 원본 상태 스냅샷
                CaptureChildStates(element);

                CaptureMouse();
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isResizing && _activeHandle != HandlePosition.None)
            {
                var element = AdornedElement as FrameworkElement;
                if (element == null) return;

                var canvas = FindCanvas();
                if (canvas == null) return;

                var currentPos = e.GetPosition(canvas);
                var deltaX = currentPos.X - _dragStart.X;
                var deltaY = currentPos.Y - _dragStart.Y;

                ApplyResize(element, deltaX, deltaY);
                InvalidateVisual();
                e.Handled = true;
            }
            else
            {
                var element = AdornedElement as FrameworkElement;
                if (element != null)
                {
                    var pos = e.GetPosition(this);
                    var handle = HitTestHandle(pos, element.ActualWidth, element.ActualHeight);
                    Cursor = GetHandleCursor(handle);
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (_isResizing)
            {
                _isResizing = false;
                _activeHandle = HandlePosition.None;
                _originalChildStates = null;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        #endregion

        #region Private Methods

        private void DrawHandle(DrawingContext dc, double x, double y, double size, double half, Pen stroke)
        {
            dc.DrawRectangle(_handleFill, stroke, new Rect(x - half, y - half, size, size));
        }

        private double GetZoomCompensation()
        {
            // visual tree를 따라 BackgroundCanvas를 검색
            DependencyObject parent = VisualTreeHelper.GetParent(AdornedElement);
            while (parent != null)
            {
                if (parent is BackgroundCanvas bgCanvas)
                {
                    var zoom = bgCanvas.ZoomLevel;
                    return zoom > 0 ? 1.0 / zoom : 1.0;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return 1.0;
        }

        /// <summary>
        /// BackgroundCanvas의 부모 뷰포트 영역으로 DrawingContext를 클리핑합니다.
        /// Adorner가 뷰포트 밖으로 렌더링되는 것을 방지합니다.
        /// </summary>
        private bool PushViewportClip(DrawingContext dc)
        {
            // BackgroundCanvas 찾기
            DependencyObject ancestor = VisualTreeHelper.GetParent(AdornedElement);
            BackgroundCanvas? bgCanvas = null;
            while (ancestor != null)
            {
                if (ancestor is BackgroundCanvas bc) { bgCanvas = bc; break; }
                ancestor = VisualTreeHelper.GetParent(ancestor);
            }
            if (bgCanvas == null) return false;

            var viewport = bgCanvas.Parent as FrameworkElement;
            if (viewport == null || viewport.ActualWidth <= 0 || viewport.ActualHeight <= 0) return false;

            try
            {
                // 뷰포트 바운드를 adorned element 좌표계로 변환
                var transform = viewport.TransformToDescendant(AdornedElement);
                var topLeft = transform.Transform(new Point(0, 0));
                var bottomRight = transform.Transform(new Point(viewport.ActualWidth, viewport.ActualHeight));
                dc.PushClip(new RectangleGeometry(new Rect(topLeft, bottomRight)));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Canvas? FindCanvas()
        {
            DependencyObject parent = VisualTreeHelper.GetParent(AdornedElement);
            while (parent != null)
            {
                if (parent is Canvas c) return c;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        /// <summary>
        /// LayeredCanvas의 자식 요소 원본 상태를 스냅샷합니다.
        /// 리사이즈 시 항상 이 원본 기준으로 스케일하여 누적 왜곡을 방지합니다.
        /// </summary>
        private void CaptureChildStates(FrameworkElement element)
        {
            _originalChildStates = null;

            if (element is LayeredCanvas layered)
            {
                _originalChildStates = new Dictionary<UIElement, Rect>();
                foreach (var child in layered.Children)
                {
                    if (child is CanvasLayer layer)
                    {
                        // 레이어 자체 저장
                        _originalChildStates[layer] = new Rect(0, 0,
                            !double.IsNaN(layer.Width) ? layer.Width : layer.ActualWidth,
                            !double.IsNaN(layer.Height) ? layer.Height : layer.ActualHeight);

                        // 레이어 내부 자식 저장
                        foreach (UIElement layerChild in layer.Children)
                        {
                            if (layerChild is FrameworkElement fe)
                            {
                                var l = Canvas.GetLeft(fe);
                                var t = Canvas.GetTop(fe);
                                _originalChildStates[fe] = new Rect(
                                    double.IsNaN(l) ? 0 : l,
                                    double.IsNaN(t) ? 0 : t,
                                    !double.IsNaN(fe.Width) ? fe.Width : fe.ActualWidth,
                                    !double.IsNaN(fe.Height) ? fe.Height : fe.ActualHeight);
                            }
                        }
                    }
                }
            }
        }

        private HandlePosition HitTestHandle(Point pos, double width, double height)
        {
            var zoomComp = GetZoomCompensation();
            var hitSize = (HandleSize * zoomComp) + 4 * zoomComp;

            if (IsNear(pos, 0, 0, hitSize)) return HandlePosition.NW;
            if (IsNear(pos, width / 2, 0, hitSize)) return HandlePosition.N;
            if (IsNear(pos, width, 0, hitSize)) return HandlePosition.NE;
            if (IsNear(pos, width, height / 2, hitSize)) return HandlePosition.E;
            if (IsNear(pos, width, height, hitSize)) return HandlePosition.SE;
            if (IsNear(pos, width / 2, height, hitSize)) return HandlePosition.S;
            if (IsNear(pos, 0, height, hitSize)) return HandlePosition.SW;
            if (IsNear(pos, 0, height / 2, hitSize)) return HandlePosition.W;

            return HandlePosition.None;
        }

        private static bool IsNear(Point pos, double x, double y, double threshold)
        {
            return Math.Abs(pos.X - x) <= threshold && Math.Abs(pos.Y - y) <= threshold;
        }

        private void ApplyResize(FrameworkElement element, double deltaX, double deltaY)
        {
            var newLeft = _originalRect.X;
            var newTop = _originalRect.Y;
            var newWidth = _originalRect.Width;
            var newHeight = _originalRect.Height;

            const double MinSize = 10;

            switch (_activeHandle)
            {
                case HandlePosition.NW:
                    newLeft = _originalRect.X + deltaX;
                    newTop = _originalRect.Y + deltaY;
                    newWidth = _originalRect.Width - deltaX;
                    newHeight = _originalRect.Height - deltaY;
                    break;
                case HandlePosition.N:
                    newTop = _originalRect.Y + deltaY;
                    newHeight = _originalRect.Height - deltaY;
                    break;
                case HandlePosition.NE:
                    newTop = _originalRect.Y + deltaY;
                    newWidth = _originalRect.Width + deltaX;
                    newHeight = _originalRect.Height - deltaY;
                    break;
                case HandlePosition.E:
                    newWidth = _originalRect.Width + deltaX;
                    break;
                case HandlePosition.SE:
                    newWidth = _originalRect.Width + deltaX;
                    newHeight = _originalRect.Height + deltaY;
                    break;
                case HandlePosition.S:
                    newHeight = _originalRect.Height + deltaY;
                    break;
                case HandlePosition.SW:
                    newLeft = _originalRect.X + deltaX;
                    newWidth = _originalRect.Width - deltaX;
                    newHeight = _originalRect.Height + deltaY;
                    break;
                case HandlePosition.W:
                    newLeft = _originalRect.X + deltaX;
                    newWidth = _originalRect.Width - deltaX;
                    break;
            }

            if (newWidth < MinSize)
            {
                newWidth = MinSize;
                if (_activeHandle == HandlePosition.NW || _activeHandle == HandlePosition.W || _activeHandle == HandlePosition.SW)
                    newLeft = _originalRect.X + _originalRect.Width - MinSize;
            }
            if (newHeight < MinSize)
            {
                newHeight = MinSize;
                if (_activeHandle == HandlePosition.NW || _activeHandle == HandlePosition.N || _activeHandle == HandlePosition.NE)
                    newTop = _originalRect.Y + _originalRect.Height - MinSize;
            }

            // 부모 bounds 제한 — 요소가 부모 영역을 초과하지 않도록
            var parentCanvas = FindCanvas();
            double parentW = double.NaN, parentH = double.NaN;

            if (parentCanvas is CanvasLayer parentLayer)
            {
                // Shape → CanvasLayer 크기 기준
                parentW = !double.IsNaN(parentLayer.Width) ? parentLayer.Width : parentLayer.ActualWidth;
                parentH = !double.IsNaN(parentLayer.Height) ? parentLayer.Height : parentLayer.ActualHeight;
            }
            else if (parentCanvas is LayeredCanvas layeredParent)
            {
                // CanvasLayer → LayeredCanvas 크기 기준
                parentW = layeredParent.ActualWidth > 0 ? layeredParent.ActualWidth : layeredParent.Width;
                parentH = layeredParent.ActualHeight > 0 ? layeredParent.ActualHeight : layeredParent.Height;
            }

            if (!double.IsNaN(parentW) && !double.IsNaN(parentH) && parentW > 0 && parentH > 0)
            {
                if (newLeft < 0) { newWidth += newLeft; newLeft = 0; }
                if (newTop < 0) { newHeight += newTop; newTop = 0; }
                if (newLeft + newWidth > parentW) newWidth = parentW - newLeft;
                if (newTop + newHeight > parentH) newHeight = parentH - newTop;

                if (newWidth < MinSize) newWidth = MinSize;
                if (newHeight < MinSize) newHeight = MinSize;
            }

            Canvas.SetLeft(element, newLeft);
            Canvas.SetTop(element, newTop);
            element.Width = newWidth;
            element.Height = newHeight;

            // LayeredCanvas 자식 비례 스케일링 (원본 스냅샷 기준)
            if (_originalChildStates != null
                && element is LayeredCanvas layered
                && _originalRect.Width > 0 && _originalRect.Height > 0)
            {
                var scaleX = newWidth / _originalRect.Width;
                var scaleY = newHeight / _originalRect.Height;

                foreach (var child in layered.Children)
                {
                    if (child is CanvasLayer layer && _originalChildStates.TryGetValue(layer, out var layerOriginal))
                    {
                        layer.Width = layerOriginal.Width * scaleX;
                        layer.Height = layerOriginal.Height * scaleY;

                        foreach (UIElement layerChild in layer.Children)
                        {
                            if (layerChild is FrameworkElement fe
                                && _originalChildStates.TryGetValue(fe, out var childOriginal))
                            {
                                Canvas.SetLeft(fe, childOriginal.X * scaleX);
                                Canvas.SetTop(fe, childOriginal.Y * scaleY);
                                fe.Width = childOriginal.Width * scaleX;
                                fe.Height = childOriginal.Height * scaleY;
                            }
                        }
                    }
                }
            }
        }

        private static Cursor GetHandleCursor(HandlePosition handle)
        {
            return handle switch
            {
                HandlePosition.N or HandlePosition.S => Cursors.SizeNS,
                HandlePosition.E or HandlePosition.W => Cursors.SizeWE,
                HandlePosition.NW or HandlePosition.SE => Cursors.SizeNWSE,
                HandlePosition.NE or HandlePosition.SW => Cursors.SizeNESW,
                _ => Cursors.Arrow
            };
        }

        private static Brush? TryFindBrush(string key)
        {
            try
            {
                return Application.Current?.TryFindResource(key) as Brush;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Enums

        private enum HandlePosition
        {
            None, N, S, E, W, NE, NW, SE, SW
        }

        #endregion
    }
}
