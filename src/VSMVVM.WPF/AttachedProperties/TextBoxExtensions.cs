using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VSMVVM.WPF.AttachedProperties
{
    /// <summary>
    /// TextBox 가 Enter 키를 받았을 때 binding source 를 즉시 commit 한다.
    /// UpdateSourceTrigger=LostFocus 와 KeyBinding{Key=Return, Command=...} 를 함께 쓰면
    /// Enter 가 LostFocus 를 발생시키지 않아 command 가 이전 값을 보는 문제가 있는데,
    /// SubmitOnEnter=True 면 KeyBinding 발화 전에 source 가 갱신되어 command 가 최신 값을 본다.
    /// </summary>
    public static class TextBoxExtensions
    {
        public static readonly DependencyProperty SubmitOnEnterProperty =
            DependencyProperty.RegisterAttached(
                "SubmitOnEnter",
                typeof(bool),
                typeof(TextBoxExtensions),
                new PropertyMetadata(false, OnSubmitOnEnterChanged));

        public static bool GetSubmitOnEnter(DependencyObject d) =>
            (bool)d.GetValue(SubmitOnEnterProperty);

        public static void SetSubmitOnEnter(DependencyObject d, bool value) =>
            d.SetValue(SubmitOnEnterProperty, value);

        private static void OnSubmitOnEnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox tb) return;
            tb.PreviewKeyDown -= OnPreviewKeyDown;
            if (e.NewValue is true)
                tb.PreviewKeyDown += OnPreviewKeyDown;
        }

        // PreviewKeyDown 단계에서 source 만 commit. e.Handled 는 건드리지 않는다 →
        // 이후 InputBindings 의 KeyBinding 이 정상 발화하고, 그 시점 ViewModel property 는 이미 최신 값.
        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return && e.Key != Key.Enter) return;
            if (sender is not TextBox tb) return;

            var expr = tb.GetBindingExpression(TextBox.TextProperty);
            expr?.UpdateSource();
        }
    }
}