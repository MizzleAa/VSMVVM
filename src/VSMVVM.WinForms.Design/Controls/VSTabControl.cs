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
    /// WPF.Design TabControl.xaml에 대응하는 커스텀 탭 컨트롤.
    /// 언더라인 스타일 탭 헤더, 선택/호버 색상 변경을 지원합니다.
    /// </summary>
    public class VSTabControl : TabControl
    {
        #region Fields

        private int _underlineHeight = Sizing.H0_5;

        #endregion

        #region Properties

        /// <summary>선택 탭 하단 악센트 라인 두께.</summary>
        [Category("Appearance")]
        [DefaultValue(2)]
        public int UnderlineHeight
        {
            get => _underlineHeight;
            set { _underlineHeight = value; Invalidate(); }
        }

        #endregion

        #region Constructor

        public VSTabControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.DoubleBuffer |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            Font = Typography.DefaultFont;
            Padding = new Point(12, 8);
            ItemSize = new Size(0, 36);
            SizeMode = TabSizeMode.Normal;

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        #endregion

        #region Theme

        private void OnThemeChanged()
        {
            if (InvokeRequired)
                BeginInvoke(new Action(() => Invalidate()));
            else
                Invalidate();
        }

        #endregion

        #region Paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            DesignRenderer.SetHighQuality(g);

            var theme = ThemeManager.Current;

            // 전체 배경
            g.Clear(theme.BgPrimary);

            // 탭 헤더 영역
            var headerBounds = GetTabHeaderBounds();

            // 탭 헤더 하단 구분선
            using var borderPen = new Pen(theme.BorderDefault);
            g.DrawLine(borderPen, headerBounds.X, headerBounds.Bottom, headerBounds.Right, headerBounds.Bottom);

            // 각 탭 그리기
            for (int i = 0; i < TabCount; i++)
            {
                DrawTab(g, i, theme);
            }

            // 콘텐츠 영역은 TabPage가 자체 렌더링
        }

        private void DrawTab(Graphics g, int index, Colors.ITheme theme)
        {
            var tabRect = GetTabRect(index);
            bool isSelected = (SelectedIndex == index);
            bool isHovered = tabRect.Contains(PointToClient(Cursor.Position));

            // 텍스트 색상
            Color textColor;
            if (isSelected)
                textColor = theme.AccentPrimary;
            else if (isHovered)
                textColor = theme.TextPrimary;
            else
                textColor = theme.TextMuted;

            // 탭 텍스트
            DesignRenderer.DrawCenteredText(g, TabPages[index].Text, Font, textColor, tabRect);

            // 선택 탭 하단 악센트 라인
            if (isSelected)
            {
                var underlineRect = new Rectangle(
                    tabRect.X + 4,
                    tabRect.Bottom - _underlineHeight,
                    tabRect.Width - 8,
                    _underlineHeight);

                DesignRenderer.FillRoundedRect(g, underlineRect, theme.AccentPrimary, Effects.RoundedSm);
            }
        }

        private Rectangle GetTabHeaderBounds()
        {
            if (TabCount == 0) return Rectangle.Empty;
            var first = GetTabRect(0);
            var last = GetTabRect(TabCount - 1);
            return new Rectangle(0, first.Y, Width, last.Bottom - first.Y);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // OnPaint에서 처리
        }

        #endregion

        #region TabPage Theme

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);

            // 선택된 TabPage에 테마 색상 적용
            if (SelectedTab != null)
            {
                var theme = ThemeManager.Current;
                SelectedTab.BackColor = theme.BgPrimary;
                SelectedTab.ForeColor = theme.TextPrimary;
            }

            Invalidate();
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);

            if (e.Control is TabPage page)
            {
                ApplyPageTheme(page);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            foreach (TabPage p in TabPages)
                ApplyPageTheme(p);
        }

        private void ApplyPageTheme(TabPage page)
        {
            var theme = ThemeManager.Current;
            page.BackColor = theme.BgPrimary;
            page.ForeColor = theme.TextPrimary;
            foreach (Control c in page.Controls)
            {
                if (c is Label)
                    c.ForeColor = theme.TextPrimary;
            }
        }

        #endregion

        #region Mouse

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Invalidate(); // 호버 효과를 위해 다시 그리기
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Invalidate();
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
