using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VSMVVM.WPF.Design.Components
{
    /// <summary>
    /// 로딩 오버레이 컨트롤.
    /// IsLoading=True 시 반투명 배경 + 스피너 애니메이션을 표시합니다.
    /// </summary>
    public class LoadingOverlay : ContentControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(
                nameof(IsLoading),
                typeof(bool),
                typeof(LoadingOverlay),
                new PropertyMetadata(false, OnIsLoadingChanged));

        public static readonly DependencyProperty SpinnerSizeProperty =
            DependencyProperty.Register(
                nameof(SpinnerSize),
                typeof(double),
                typeof(LoadingOverlay),
                new PropertyMetadata(40.0));

        public static readonly DependencyProperty SpinnerStrokeProperty =
            DependencyProperty.Register(
                nameof(SpinnerStroke),
                typeof(double),
                typeof(LoadingOverlay),
                new PropertyMetadata(3.0));

        public static readonly DependencyProperty OverlayBackgroundProperty =
            DependencyProperty.Register(
                nameof(OverlayBackground),
                typeof(Brush),
                typeof(LoadingOverlay),
                new PropertyMetadata(null));

        public static readonly DependencyProperty LoadingTextProperty =
            DependencyProperty.Register(
                nameof(LoadingText),
                typeof(string),
                typeof(LoadingOverlay),
                new PropertyMetadata(null));

        #endregion

        #region Properties

        /// <summary>로딩 상태 (True 시 오버레이 표시)</summary>
        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        /// <summary>스피너 크기 (px)</summary>
        public double SpinnerSize
        {
            get => (double)GetValue(SpinnerSizeProperty);
            set => SetValue(SpinnerSizeProperty, value);
        }

        /// <summary>스피너 선 두께 (px)</summary>
        public double SpinnerStroke
        {
            get => (double)GetValue(SpinnerStrokeProperty);
            set => SetValue(SpinnerStrokeProperty, value);
        }

        /// <summary>오버레이 배경 브러시</summary>
        public Brush OverlayBackground
        {
            get => (Brush)GetValue(OverlayBackgroundProperty);
            set => SetValue(OverlayBackgroundProperty, value);
        }

        /// <summary>로딩 텍스트 (선택)</summary>
        public string LoadingText
        {
            get => (string)GetValue(LoadingTextProperty);
            set => SetValue(LoadingTextProperty, value);
        }

        #endregion

        static LoadingOverlay()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(LoadingOverlay),
                new FrameworkPropertyMetadata(typeof(LoadingOverlay)));
        }

        private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LoadingOverlay overlay)
            {
                overlay.UpdateOverlayVisibility();
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            UpdateOverlayVisibility();
        }

        private void UpdateOverlayVisibility()
        {
            var overlay = GetTemplateChild("PART_Overlay") as UIElement;
            var spinner = GetTemplateChild("PART_Spinner") as UIElement;
            var loadingText = GetTemplateChild("PART_LoadingText") as System.Windows.Controls.TextBlock;

            if (overlay != null)
            {
                overlay.Visibility = IsLoading ? Visibility.Visible : Visibility.Collapsed;
            }

            if (loadingText != null)
            {
                loadingText.Visibility = string.IsNullOrEmpty(LoadingText)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            if (spinner != null && IsLoading)
            {
                var rotation = new RotateTransform();
                spinner.RenderTransform = rotation;
                spinner.RenderTransformOrigin = new Point(0.5, 0.5);

                var animation = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromSeconds(1),
                    RepeatBehavior = RepeatBehavior.Forever
                };

                rotation.BeginAnimation(RotateTransform.AngleProperty, animation);
            }
        }
    }
}
