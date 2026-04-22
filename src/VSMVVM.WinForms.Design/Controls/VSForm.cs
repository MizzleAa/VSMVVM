using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// WPF.Design Window.xaml에 대응하는 커스텀 Chrome Form.
    /// 커스텀 타이틀바, 둥근 모서리, 시스템 버튼(최소화/최대화/닫기)을 제공합니다.
    /// </summary>
    public class VSForm : Form
    {
        #region Win32

        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        #endregion

        #region Fields

        private const int TitleBarHeight = 36;
        private const int ResizeBorder = 6;
        private const int SystemButtonWidth = 44;
        private int _cornerRadius = Effects.RoundedLg;

        private Rectangle _minimizeBtnRect;
        private Rectangle _maximizeBtnRect;
        private Rectangle _closeBtnRect;
        private string _hoveredButton = null;
        private ThemeSubscription _themeSub;

        #endregion

        #region Properties

        /// <summary>모서리 반경.</summary>
        [Category("Appearance")]
        [DefaultValue(8)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; UpdateRegion(); Invalidate(); }
        }

        #endregion

        #region Constructor

        public VSForm()
        {
            ControlStyleHelper.ApplyVSDefaultStyles(this, supportTransparent: false);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Font = Typography.DefaultFont;

            _themeSub = new ThemeSubscription(this, ApplyTheme);
            ApplyTheme();
            UpdateRegion();
        }

        #endregion

        #region Theme

        private void ApplyTheme()
        {
            var theme = ThemeManager.Current;
            BackColor = theme.BgPrimary;
            ForeColor = theme.TextPrimary;
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

            // 전체 배경 (둥근 모서리)
            if (WindowState == FormWindowState.Normal)
            {
                DesignRenderer.FillRoundedRect(g, bounds, theme.BgPrimary, _cornerRadius);
                DesignRenderer.DrawRoundedRect(g, bounds, theme.BorderDefault, _cornerRadius);
            }
            else
            {
                g.Clear(theme.BgPrimary);
            }

            // 타이틀바
            var titleBarRect = new Rectangle(0, 0, Width, TitleBarHeight);
            if (WindowState == FormWindowState.Normal)
            {
                // 상단 둥근 모서리만 적용
                using var path = CreateTopRoundedRect(titleBarRect, _cornerRadius);
                using var brush = new SolidBrush(theme.BgSecondary);
                g.FillPath(brush, path);
            }
            else
            {
                using var brush = new SolidBrush(theme.BgSecondary);
                g.FillRectangle(brush, titleBarRect);
            }

            // 타이틀바 하단 구분선
            using var borderPen = new Pen(theme.BorderMuted);
            g.DrawLine(borderPen, 0, TitleBarHeight, Width, TitleBarHeight);

            // 아이콘 + 타이틀 텍스트
            int textX = 12;
            if (Icon != null)
            {
                g.DrawIcon(Icon, new Rectangle(8, (TitleBarHeight - 16) / 2, 16, 16));
                textX = 30;
            }
            var titleRect = new Rectangle(textX, 0, Width - textX - SystemButtonWidth * 3, TitleBarHeight);
            DesignRenderer.DrawLeftText(g, Text, Typography.MediumFont, theme.TextPrimary, titleRect);

            // 시스템 버튼 영역 계산
            _closeBtnRect = new Rectangle(Width - SystemButtonWidth, 0, SystemButtonWidth, TitleBarHeight);
            _maximizeBtnRect = new Rectangle(Width - SystemButtonWidth * 2, 0, SystemButtonWidth, TitleBarHeight);
            _minimizeBtnRect = new Rectangle(Width - SystemButtonWidth * 3, 0, SystemButtonWidth, TitleBarHeight);

            // 시스템 버튼 드로잉
            DrawSystemButton(g, _minimizeBtnRect, "minimize", theme);
            DrawSystemButton(g, _maximizeBtnRect, "maximize", theme);
            DrawSystemButton(g, _closeBtnRect, "close", theme);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // OnPaint에서 처리
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        /// <summary>
        /// OS 레벨 윈도우 영역을 둥근 사각형으로 클리핑합니다.
        /// Normal 상태에서만 적용하고, Maximized시 사각형으로 복원합니다.
        /// </summary>
        private void UpdateRegion()
        {
            if (WindowState == FormWindowState.Normal && _cornerRadius > 0)
            {
                using var path = DesignRenderer.CreateRoundedRect(new Rectangle(0, 0, Width, Height), _cornerRadius);
                Region = new System.Drawing.Region(path);
            }
            else
            {
                Region = null;
            }
        }

        private void DrawSystemButton(Graphics g, Rectangle rect, string type, Colors.ITheme theme)
        {
            // 호버 배경 (WPF: Close→Error, 나머지→BgTertiary)
            if (_hoveredButton == type)
            {
                var hoverColor = type == "close" ? theme.Error : theme.BgTertiary;
                using var brush = new SolidBrush(hoverColor);
                g.FillRectangle(brush, rect);
            }

            // WPF Path 기하 (Window.xaml):
            //   minimize: M4,0 L16,0            → 너비 12, StrokeThickness 2
            //   maximize: M1,1 L1,11 L11,11 L11,1 Z → 10×10 사각형
            //   close:    M0,0 L10,10 M10,0 L0,10 → 10×10 X
            //   StrokeThickness = H0_5 = 2
            var iconColor = _hoveredButton == "close" ? theme.TextPrimary : theme.TextSecondary;
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;

            using var pen = new Pen(iconColor, 2f);
            pen.StartCap = LineCap.Flat;
            pen.EndCap = LineCap.Flat;

            switch (type)
            {
                case "minimize":
                    // 가로 라인 12px
                    g.DrawLine(pen, cx - 6, cy, cx + 6, cy);
                    break;

                case "maximize":
                    if (WindowState == FormWindowState.Maximized)
                    {
                        // Restore: 겹친 사각형 (WPF와 동일한 형태)
                        g.DrawRectangle(pen, cx - 3, cy - 5, 8, 8);
                        g.DrawRectangle(pen, cx - 5, cy - 3, 8, 8);
                    }
                    else
                    {
                        // 10×10 사각형 (WPF: M1,1 L1,11 L11,11 L11,1 Z의 중앙 정렬)
                        g.DrawRectangle(pen, cx - 5, cy - 5, 10, 10);
                    }
                    break;

                case "close":
                    // 10×10 X
                    g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                    g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                    break;
            }
        }

        private GraphicsPath CreateTopRoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2f;

            // Top-Left arc
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            // Top-Right arc
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            // Bottom-Right (flat)
            path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);

            path.CloseFigure();
            return path;
        }

        #endregion

        #region Mouse — System Buttons & Resize

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            string oldHovered = _hoveredButton;
            _hoveredButton = null;

            if (_closeBtnRect.Contains(e.Location))
                _hoveredButton = "close";
            else if (_maximizeBtnRect.Contains(e.Location))
                _hoveredButton = "maximize";
            else if (_minimizeBtnRect.Contains(e.Location))
                _hoveredButton = "minimize";

            if (oldHovered != _hoveredButton)
                Invalidate(new Rectangle(Width - SystemButtonWidth * 3, 0, SystemButtonWidth * 3, TitleBarHeight));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredButton != null)
            {
                _hoveredButton = null;
                Invalidate(new Rectangle(Width - SystemButtonWidth * 3, 0, SystemButtonWidth * 3, TitleBarHeight));
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left) return;

            if (_closeBtnRect.Contains(e.Location))
            {
                Close();
            }
            else if (_maximizeBtnRect.Contains(e.Location))
            {
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            }
            else if (_minimizeBtnRect.Contains(e.Location))
            {
                WindowState = FormWindowState.Minimized;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            // 타이틀바 더블클릭으로 최대화/복원
            if (e.Y < TitleBarHeight && !_closeBtnRect.Contains(e.Location)
                && !_maximizeBtnRect.Contains(e.Location) && !_minimizeBtnRect.Contains(e.Location))
            {
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            }
        }

        #endregion

        #region WndProc — Resize & Drag

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                var point = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));

                if (WindowState == FormWindowState.Normal)
                {
                    // 리사이즈 영역
                    if (point.X < ResizeBorder && point.Y < ResizeBorder)
                    { m.Result = (IntPtr)HTTOPLEFT; return; }
                    if (point.X > Width - ResizeBorder && point.Y < ResizeBorder)
                    { m.Result = (IntPtr)HTTOPRIGHT; return; }
                    if (point.X < ResizeBorder && point.Y > Height - ResizeBorder)
                    { m.Result = (IntPtr)HTBOTTOMLEFT; return; }
                    if (point.X > Width - ResizeBorder && point.Y > Height - ResizeBorder)
                    { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                    if (point.X < ResizeBorder)
                    { m.Result = (IntPtr)HTLEFT; return; }
                    if (point.X > Width - ResizeBorder)
                    { m.Result = (IntPtr)HTRIGHT; return; }
                    if (point.Y < ResizeBorder)
                    { m.Result = (IntPtr)HTTOP; return; }
                    if (point.Y > Height - ResizeBorder)
                    { m.Result = (IntPtr)HTBOTTOM; return; }
                }

                // 타이틀바 영역 (시스템 버튼 제외) → 드래그 이동
                if (point.Y < TitleBarHeight && point.X < Width - SystemButtonWidth * 3)
                {
                    // AppBar 같은 예약 영역은 HTCLIENT로 돌려서 클릭을 자식 컨트롤로 전달
                    foreach (var r in ReservedClientRects)
                    {
                        if (r.Contains(point))
                        {
                            m.Result = (IntPtr)HTCLIENT;
                            return;
                        }
                    }

                    m.Result = (IntPtr)HTCAPTION;
                    return;
                }

                m.Result = (IntPtr)HTCLIENT;
                return;
            }

            base.WndProc(ref m);
        }

        #endregion

        #region Content Area

        /// <summary>
        /// 타이틀바를 제외한 콘텐츠 영역의 Padding을 반환합니다.
        /// 파생 클래스에서 컨트롤을 배치할 때 이 값을 사용하여 타이틀바 아래에 배치합니다.
        /// </summary>
        [Browsable(false)]
        public Padding ContentPadding => new Padding(0, TitleBarHeight, 0, 0);

        /// <summary>
        /// 타이틀바 영역 안에서 드래그(HTCAPTION) 대신 클라이언트 클릭(HTCLIENT)으로 처리할 사각형 목록.
        /// 파생 폼에서 AppBar 버튼처럼 타이틀바에 배치된 인터랙티브 영역을 override로 노출합니다.
        /// 좌표는 form-client 기준이며 각 Rectangle이 현재 위치/크기를 반영해야 합니다.
        /// </summary>
        [Browsable(false)]
        protected virtual IEnumerable<Rectangle> ReservedClientRects => Array.Empty<Rectangle>();

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
