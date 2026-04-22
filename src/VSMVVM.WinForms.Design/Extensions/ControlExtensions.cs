using System.Windows.Forms;
using VSMVVM.WinForms.Design.Core;

namespace VSMVVM.WinForms.Design.Extensions
{
    /// <summary>
    /// 표준 WinForms 컨트롤에 VSMVVM 테마를 일괄 적용하는 확장 메서드.
    /// VS커스텀 컨트롤이 아닌 기본 컨트롤에도 색상을 적용할 수 있습니다.
    /// </summary>
    public static class ControlExtensions
    {
        /// <summary>
        /// 컨트롤과 모든 자식 컨트롤에 현재 테마를 재귀적으로 적용합니다.
        /// </summary>
        public static void ApplyVSTheme(this Control control)
        {
            ThemeManager.Apply(control);
        }

        /// <summary>
        /// Form에 VSMVVM 디자인 기본값을 적용합니다.
        /// (BackColor, ForeColor, Font 설정)
        /// </summary>
        public static void ApplyVSDefaults(this Form form)
        {
            var theme = ThemeManager.Current;
            form.BackColor = theme.BgPrimary;
            form.ForeColor = theme.TextPrimary;
            form.Font = Tokens.Typography.DefaultFont;
            ThemeManager.Apply(form);
        }
    }
}
