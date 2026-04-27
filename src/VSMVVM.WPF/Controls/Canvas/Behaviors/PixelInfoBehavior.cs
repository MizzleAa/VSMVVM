using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

#nullable enable
namespace VSMVVM.WPF.Controls.Behaviors
{
    /// <summary>
    /// <see cref="LayeredCanvas"/>에 부착해 마우스 위치를 MaskLayer 픽셀 좌표로 변환,
    /// 해당 위치의 RGB 와 함께 OneWayToSource DP 로 ViewModel 에 노출한다.
    /// </summary>
    /// <remarks>
    /// <see cref="MouseTrackElement"/> 패턴(<c>handledEventsToo:true</c>) 으로 도구가 Handled 한 이벤트도 수신.
    /// </remarks>
    public sealed class PixelInfoBehavior : Behavior<LayeredCanvas>
    {
        #region DependencyProperties

        public static readonly DependencyProperty MaskLayerProperty =
            DependencyProperty.Register(
                nameof(MaskLayer),
                typeof(MaskLayer),
                typeof(PixelInfoBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty MouseImageXProperty =
            DependencyProperty.Register(
                nameof(MouseImageX),
                typeof(int),
                typeof(PixelInfoBehavior),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty MouseImageYProperty =
            DependencyProperty.Register(
                nameof(MouseImageY),
                typeof(int),
                typeof(PixelInfoBehavior),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty MouseRgbProperty =
            DependencyProperty.Register(
                nameof(MouseRgb),
                typeof(Color?),
                typeof(PixelInfoBehavior),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty IsMouseInImageProperty =
            DependencyProperty.Register(
                nameof(IsMouseInImage),
                typeof(bool),
                typeof(PixelInfoBehavior),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>RGB 를 읽을 대상 MaskLayer.</summary>
        public MaskLayer? MaskLayer
        {
            get => (MaskLayer?)GetValue(MaskLayerProperty);
            set => SetValue(MaskLayerProperty, value);
        }

        /// <summary>마우스 아래 이미지 픽셀 X (0 기반).</summary>
        public int MouseImageX
        {
            get => (int)GetValue(MouseImageXProperty);
            set => SetValue(MouseImageXProperty, value);
        }

        /// <summary>마우스 아래 이미지 픽셀 Y (0 기반).</summary>
        public int MouseImageY
        {
            get => (int)GetValue(MouseImageYProperty);
            set => SetValue(MouseImageYProperty, value);
        }

        /// <summary>마우스 아래 픽셀 RGB. SourceImage 미설정 시 null.</summary>
        public Color? MouseRgb
        {
            get => (Color?)GetValue(MouseRgbProperty);
            set => SetValue(MouseRgbProperty, value);
        }

        /// <summary>마우스가 유효한 이미지 픽셀 위에 있는지.</summary>
        public bool IsMouseInImage
        {
            get => (bool)GetValue(IsMouseInImageProperty);
            set => SetValue(IsMouseInImageProperty, value);
        }

        #endregion

        protected override void OnAttached()
        {
            base.OnAttached();
            // handledEventsToo=true: 그리기 도구가 Handled=true 로 마킹해도 픽셀 정보는 갱신.
            AssociatedObject.AddHandler(UIElement.MouseMoveEvent, (MouseEventHandler)OnMouseMove, handledEventsToo: true);
            AssociatedObject.AddHandler(UIElement.MouseLeaveEvent, (MouseEventHandler)OnMouseLeave, handledEventsToo: true);
        }

        protected override void OnDetaching()
        {
            AssociatedObject.RemoveHandler(UIElement.MouseMoveEvent, (MouseEventHandler)OnMouseMove);
            AssociatedObject.RemoveHandler(UIElement.MouseLeaveEvent, (MouseEventHandler)OnMouseLeave);
            base.OnDetaching();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var canvas = AssociatedObject;
            var mask = MaskLayer;
            if (mask == null || mask.MaskWidth <= 0 || mask.MaskHeight <= 0) { Reset(); return; }

            var screenPos = e.GetPosition(canvas);
            var canvasPos = canvas.ScreenToCanvas(screenPos);

            double maskLeft = Canvas.GetLeft(mask); if (double.IsNaN(maskLeft)) maskLeft = 0;
            double maskTop = Canvas.GetTop(mask); if (double.IsNaN(maskTop)) maskTop = 0;
            double displayW = mask.ActualWidth > 0 ? mask.ActualWidth : mask.MaskWidth;
            double displayH = mask.ActualHeight > 0 ? mask.ActualHeight : mask.MaskHeight;
            if (displayW <= 0 || displayH <= 0) { Reset(); return; }

            int px = (int)Math.Floor((canvasPos.X - maskLeft) * mask.MaskWidth / displayW);
            int py = (int)Math.Floor((canvasPos.Y - maskTop) * mask.MaskHeight / displayH);

            if ((uint)px >= (uint)mask.MaskWidth || (uint)py >= (uint)mask.MaskHeight) { Reset(); return; }

            MouseImageX = px;
            MouseImageY = py;
            var rgb = mask.GetSourcePixelRgb(px, py);
            MouseRgb = rgb is { } v ? Color.FromRgb(v.R, v.G, v.B) : (Color?)null;
            IsMouseInImage = true;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e) => Reset();

        private void Reset()
        {
            if (IsMouseInImage) IsMouseInImage = false;
            if (MouseRgb != null) MouseRgb = null;
        }
    }
}
