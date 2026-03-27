using System.Windows;
using System.Windows.Controls;

namespace VSMVVM.WPF.Design.Core
{
    /// <summary>
    /// Window 타이틀바에 사용자 지정 버튼을 추가하기 위한 Attached Property.
    /// 최소화 버튼 왼쪽에 CustomButtons 영역을 제공합니다.
    /// </summary>
    public static class WindowChrome
    {
        #region CustomButtons AP

        public static readonly DependencyProperty CustomButtonsProperty =
            DependencyProperty.RegisterAttached(
                "CustomButtons",
                typeof(UIElement),
                typeof(WindowChrome),
                new FrameworkPropertyMetadata(null));

        public static void SetCustomButtons(DependencyObject element, UIElement value)
        {
            element.SetValue(CustomButtonsProperty, value);
        }

        public static UIElement GetCustomButtons(DependencyObject element)
        {
            return (UIElement)element.GetValue(CustomButtonsProperty);
        }

        #endregion

        #region TitleContent AP

        public static readonly DependencyProperty TitleContentProperty =
            DependencyProperty.RegisterAttached(
                "TitleContent",
                typeof(object),
                typeof(WindowChrome),
                new FrameworkPropertyMetadata(null));

        public static void SetTitleContent(DependencyObject element, object value)
        {
            element.SetValue(TitleContentProperty, value);
        }

        public static object GetTitleContent(DependencyObject element)
        {
            return element.GetValue(TitleContentProperty);
        }

        #endregion
    }
}
