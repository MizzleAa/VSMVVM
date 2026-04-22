using System;
using System.Drawing;
using System.Windows.Forms;
using VSMVVM.WinForms.Design.Colors;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Core
{
    /// <summary>
    /// 모든 VSMVVM WinForms 커스텀 컨트롤의 기본 클래스.
    /// 테마 자동 구독, 더블 버퍼링, 마우스 상태 추적을 제공합니다.
    /// </summary>
    public abstract class VSControlBase : Control
    {
        #region Fields

        private bool _isHovered;
        private bool _isPressed;
        private ThemeSubscription _themeSub;

        #endregion

        #region Properties

        /// <summary>마우스가 컨트롤 위에 있는지 여부.</summary>
        protected bool IsHovered => _isHovered;

        /// <summary>마우스 버튼이 눌려있는지 여부.</summary>
        protected bool IsPressed => _isPressed;

        /// <summary>현재 활성 테마 단축 접근.</summary>
        protected ITheme Theme => ThemeManager.Current;

        #endregion

        #region Constructor

        protected VSControlBase()
        {
            ControlStyleHelper.ApplyVSDefaultStyles(this, supportTransparent: true);

            BackColor = Color.Transparent;
            Font = Typography.DefaultFont;
            _themeSub = new ThemeSubscription(this, ApplyTheme);
        }

        #endregion

        #region Paint Background

        /// <summary>
        /// 투명 배경 지원: 부모 체인을 거슬러 올라가 첫 번째 불투명 배경색으로 채웁니다.
        /// Parent.BackColor 자체가 Transparent이면 SolidBrush(Transparent)는 아무것도 그리지 않아
        /// 이전 상태 픽셀이 남는 ghost 현상이 발생하므로 반드시 실제 색을 찾아야 합니다.
        /// </summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (BackColor == Color.Transparent)
            {
                var effective = ResolveEffectiveBackColor();
                using var brush = new SolidBrush(effective);
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
            else
            {
                base.OnPaintBackground(e);
            }
        }

        private Color ResolveEffectiveBackColor()
        {
            var p = Parent;
            while (p != null && p.BackColor == Color.Transparent)
            {
                p = p.Parent;
            }
            return p?.BackColor ?? ThemeManager.Current.BgPrimary;
        }

        #endregion

        #region Region Clipping

        /// <summary>
        /// 이 컨트롤의 Region을 둥근 사각형으로 갱신합니다.
        /// 파생 클래스가 OnResize / CornerRadius setter에서 호출합니다.
        /// </summary>
        protected void UpdateRoundedRegion(int cornerRadius)
        {
            DesignRenderer.ApplyRoundedRegion(this, cornerRadius);
        }

        #endregion

        #region Theme

        /// <summary>
        /// 테마 변경 시 호출됩니다. 파생 클래스에서 오버라이드하여 추가 로직을 수행할 수 있습니다.
        /// </summary>
        public virtual void ApplyTheme()
        {
            Invalidate();
        }

        #endregion

        #region Mouse State Tracking

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            _isPressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isPressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        #endregion

        #region Dispose

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _themeSub?.Dispose();
                _themeSub = null;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
