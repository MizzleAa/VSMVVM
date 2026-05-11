using System.Windows;
using Microsoft.Xaml.Behaviors;

#nullable enable
namespace VSMVVM.WPF.Controls.Behaviors
{
    /// <summary>
    /// <see cref="IZoomPanViewport"/>를 구현한 컨트롤에 부착해 VM이 <see cref="FitTrigger"/>
    /// 값을 바꾸면 <see cref="IZoomPanViewport.FitToContent"/>를 호출하도록 한다.
    /// </summary>
    /// <remarks>
    /// VM은 이벤트 대신 프로퍼티 변경으로 동작을 요청한다. 토큰 값이 다르기만 하면 되므로
    /// <c>object</c>에 <c>new object()</c>나 카운터를 할당해 트리거한다.
    /// AssociatedObject가 <see cref="IZoomPanViewport"/>를 구현하지 않으면 무시한다.
    /// </remarks>
    public sealed class FitToContentBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty FitTriggerProperty =
            DependencyProperty.Register(
                nameof(FitTrigger),
                typeof(object),
                typeof(FitToContentBehavior),
                new PropertyMetadata(null, OnFitTriggerChanged));

        public static readonly DependencyProperty ZoomToBoundsTriggerProperty =
            DependencyProperty.Register(
                nameof(ZoomToBoundsTrigger),
                typeof(Rect?),
                typeof(FitToContentBehavior),
                new PropertyMetadata(null, OnZoomToBoundsTriggerChanged));

        /// <summary>값이 바뀔 때마다 Fit이 실행된다. 값 자체에는 의미가 없다.</summary>
        public object? FitTrigger
        {
            get => GetValue(FitTriggerProperty);
            set => SetValue(FitTriggerProperty, value);
        }

        /// <summary>
        /// 값이 바뀔 때마다 지정된 <see cref="Rect"/> 영역으로 zoom + center 한다.
        /// null 이거나 빈 Rect 이면 아무 동작 하지 않는다.
        /// </summary>
        public Rect? ZoomToBoundsTrigger
        {
            get => (Rect?)GetValue(ZoomToBoundsTriggerProperty);
            set => SetValue(ZoomToBoundsTriggerProperty, value);
        }

        private static void OnFitTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FitToContentBehavior self) return;
            if (self.AssociatedObject is not IZoomPanViewport vp) return;
            // VM 이 BackgroundImageSource / ImagePixelWidth / Height 를 갱신한 직후 FitTrigger 를 발화하면
            // Layout pass 가 끝나기 전에 vp.FitToContent() 가 옛 viewport 크기로 계산되어 fit 이 안 맞을 수 있다.
            // ContextIdle 우선순위로 미루면 layout/render pass 가 모두 끝난 뒤 호출되어 새 ImageSource 크기 기준으로 정확히 fit.
            self.AssociatedObject.Dispatcher.BeginInvoke(
                new System.Action(() => vp.FitToContent()),
                System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private static void OnZoomToBoundsTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FitToContentBehavior self) return;
            if (self.AssociatedObject is not IZoomPanViewport vp) return;
            if (e.NewValue is not Rect r || r.IsEmpty || r.Width <= 0 || r.Height <= 0) return;
            vp.ZoomToBounds(r);
        }
    }
}
