using System.Windows.Forms;
using System.Reflection;

namespace VSMVVM.WinForms.Design.Core
{
    /// <summary>
    /// 모든 VS* 커스텀 컨트롤이 동일하게 사용하는 SetStyle 플래그 세트를 한 곳에서 관리.
    /// Control.SetStyle은 protected이므로 리플렉션으로 호출합니다.
    /// </summary>
    public static class ControlStyleHelper
    {
        private static readonly MethodInfo SetStyleMethod =
            typeof(Control).GetMethod("SetStyle", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// VS* 컨트롤 표준 스타일을 적용합니다.
        /// </summary>
        /// <param name="control">대상 컨트롤.</param>
        /// <param name="supportTransparent">
        /// true면 SupportsTransparentBackColor를 포함합니다.
        /// Form은 이 플래그를 지원하지 않으므로 false로 전달합니다.
        /// </param>
        public static void ApplyVSDefaultStyles(Control control, bool supportTransparent)
        {
            if (control == null) return;

            var flags =
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.DoubleBuffer |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw;

            if (supportTransparent)
            {
                flags |= ControlStyles.SupportsTransparentBackColor;
            }

            SetStyleMethod?.Invoke(control, new object[] { flags, true });
        }
    }
}
