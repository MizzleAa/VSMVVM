using System;
using System.Windows.Forms;
using VSMVVM.WinForms.Design.Colors;

namespace VSMVVM.WinForms.Design.Core
{
    /// <summary>
    /// 런타임 테마 관리자. 전역 테마 인스턴스를 관리하고 테마 전환을 지원합니다.
    /// WPF.Design에서 ThemeDark.xaml / ThemeLight.xaml을 Application.Resources에서 교체하는 것에 대응합니다.
    /// </summary>
    public static class ThemeManager
    {
        #region Fields

        private static ITheme _current = new ThemeDark();

        #endregion

        #region Properties

        /// <summary>
        /// 현재 활성 테마.
        /// </summary>
        public static ITheme Current => _current;

        #endregion

        #region Events

        /// <summary>
        /// 테마가 변경될 때 발생하는 이벤트.
        /// </summary>
        public static event Action ThemeChanged;

        #endregion

        #region Public Methods

        /// <summary>
        /// 테마를 변경합니다. 변경 후 ThemeChanged 이벤트가 발생합니다.
        /// </summary>
        public static void SetTheme(ITheme theme)
        {
            _current = theme ?? throw new ArgumentNullException(nameof(theme));
            ThemeChanged?.Invoke();
        }

        /// <summary>
        /// Dark 테마로 전환합니다.
        /// </summary>
        public static void SetDarkTheme()
        {
            SetTheme(new ThemeDark());
        }

        /// <summary>
        /// Light 테마로 전환합니다.
        /// </summary>
        public static void SetLightTheme()
        {
            SetTheme(new ThemeLight());
        }

        /// <summary>
        /// 지정된 컨트롤과 모든 자식 컨트롤에 현재 테마를 재귀적으로 적용합니다.
        /// VSControlBase 파생 컨트롤은 자동 갱신되며, 표준 컨트롤은 BackColor/ForeColor만 적용됩니다.
        /// </summary>
        public static void Apply(Control root)
        {
            if (root == null) return;

            ApplyToControl(root);

            foreach (Control child in root.Controls)
            {
                Apply(child);
            }
        }

        #endregion

        #region Private Methods

        private static void ApplyToControl(Control control)
        {
            if (control is VSControlBase vsControl)
            {
                vsControl.ApplyTheme();
                return;
            }

            // 표준 WinForms 컨트롤에 기본 색상만 적용
            control.BackColor = _current.BgPrimary;
            control.ForeColor = _current.TextPrimary;
        }

        #endregion
    }
}
