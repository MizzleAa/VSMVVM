using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// WPF.Design Panels.xaml에 대응하는 카드/그룹 패널.
    /// 둥근 모서리 + 테두리 + 배경색을 가진 컨테이너.
    /// </summary>
    public class VSPanel : Panel
    {
        #region Fields

        private int _cornerRadius = Effects.RoundedLg;
        private bool _showBorder = true;
        private ThemeSubscription _themeSub;

        #endregion

        #region Properties

        /// <summary>모서리 반경.</summary>
        [Category("Appearance")]
        [DefaultValue(8)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; DesignRenderer.ApplyRoundedRegion(this, _cornerRadius); Invalidate(); }
        }

        /// <summary>테두리 표시 여부.</summary>
        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowBorder
        {
            get => _showBorder;
            set { _showBorder = value; Invalidate(); }
        }

        #endregion

        #region Constructor

        public VSPanel()
        {
            ControlStyleHelper.ApplyVSDefaultStyles(this, supportTransparent: true);

            Padding = Spacing.P4;
            _themeSub = new ThemeSubscription(this, ApplyTheme);
            ApplyTheme();
            DesignRenderer.ApplyRoundedRegion(this, _cornerRadius);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DesignRenderer.ApplyRoundedRegion(this, _cornerRadius);
        }

        #endregion

        #region Theme

        private void ApplyTheme()
        {
            BackColor = ThemeManager.Current.BgSecondary;
            ForeColor = ThemeManager.Current.TextPrimary;
            Invalidate();
        }

        #endregion

        #region Paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            DesignRenderer.SetHighQuality(g);

            var theme = ThemeManager.Current;
            var bounds = ClientRectangle;

            // 배경
            DesignRenderer.FillRoundedRect(g, bounds, theme.BgSecondary, _cornerRadius);

            // 테두리
            if (_showBorder)
            {
                DesignRenderer.DrawRoundedRect(g, bounds, theme.BorderMuted, _cornerRadius);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Region이 모서리 바깥을 OS-level에서 잘라내므로 기본 배경 fill만으로 충분.
            base.OnPaintBackground(e);
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
