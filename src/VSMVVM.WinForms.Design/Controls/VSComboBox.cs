using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// WPF.Design ComboBox.xaml에 대응하는 커스텀 콤보박스.
    /// Owner-Draw 기반으로 둥근 모서리, 호버/포커스 테두리, 드롭다운 화살표를 지원합니다.
    /// </summary>
    public class VSComboBox : ComboBox
    {
        #region Fields

        private int _cornerRadius = Effects.RoundedMd;
        private bool _isHovered;

        #endregion

        #region Properties

        /// <summary>모서리 반경.</summary>
        [Category("Appearance")]
        [DefaultValue(6)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; Invalidate(); }
        }

        #endregion

        #region Constructor

        public VSComboBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            Font = Typography.DefaultFont;
            ItemHeight = 28;
            Size = new Size(200, 32);

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme();
        }

        #endregion

        #region Theme

        private void ApplyTheme()
        {
            var theme = ThemeManager.Current;
            BackColor = theme.BgTertiary;
            ForeColor = theme.TextPrimary;
            Invalidate();
        }

        private void OnThemeChanged()
        {
            if (InvokeRequired)
                BeginInvoke(new Action(ApplyTheme));
            else
                ApplyTheme();
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
            DesignRenderer.FillRoundedRect(g, bounds, theme.BgTertiary, _cornerRadius);

            // 테두리
            var borderColor = DroppedDown ? theme.BorderFocus :
                              _isHovered ? theme.BorderHover :
                              theme.BorderDefault;
            DesignRenderer.DrawRoundedRect(g, bounds, borderColor, _cornerRadius);

            // 선택된 텍스트
            var textBounds = new Rectangle(8, 0, bounds.Width - 30, bounds.Height);
            var text = SelectedItem?.ToString() ?? "";
            DesignRenderer.DrawLeftText(g, text, Font, theme.TextPrimary, textBounds);

            // 드롭다운 화살표
            DrawArrow(g, theme);

            // Disabled
            if (!Enabled)
            {
                using var overlay = new SolidBrush(Color.FromArgb(128, theme.BgPrimary));
                using var path = DesignRenderer.CreateRoundedRect(bounds, _cornerRadius);
                g.FillPath(overlay, path);
            }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var theme = ThemeManager.Current;
            var g = e.Graphics;
            DesignRenderer.SetHighQuality(g);

            var bounds = e.Bounds;
            var isSelected = (e.State & DrawItemState.Selected) != 0;

            // 아이템 배경
            var bgColor = isSelected ? theme.AccentPrimaryMuted : theme.BgSecondary;
            using var bgBrush = new SolidBrush(bgColor);
            g.FillRectangle(bgBrush, bounds);

            // 아이템 텍스트
            var fgColor = isSelected ? theme.AccentPrimary : theme.TextPrimary;
            var textRect = new Rectangle(bounds.X + 8, bounds.Y, bounds.Width - 8, bounds.Height);
            DesignRenderer.DrawLeftText(g, Items[e.Index].ToString(), Font, fgColor, textRect);
        }

        private void DrawArrow(Graphics g, Colors.ITheme theme)
        {
            float cx = Width - 15;
            float cy = Height / 2f;

            using var pen = new Pen(theme.TextMuted, 1.5f);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
            g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
        }

        #endregion

        #region Mouse

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        #endregion

        #region Dispose

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
