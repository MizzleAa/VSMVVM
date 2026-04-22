using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// WPF.Design ListBox.xaml에 대응하는 커스텀 리스트박스.
    /// Owner-Draw 기반으로 둥근 모서리, 호버/선택 아이템 스타일을 지원합니다.
    /// 네이티브 세로 스크롤바 영역을 WM_NCCALCSIZE 에서 제거하고 VSScrollBar 오버레이를 사용합니다.
    /// </summary>
    public class VSListBox : ListBox
    {
        #region Fields

        private int _cornerRadius = Effects.RoundedMd;
        private VSScrollBar _scrollBar;
        private bool _syncingScroll;

        private const int ScrollBarWidth = 8;
        private const int ScrollBarRightInset = 2;
        private const int LB_ADDSTRING = 0x0180;
        private const int LB_INSERTSTRING = 0x0181;
        private const int LB_DELETESTRING = 0x0182;
        private const int LB_RESETCONTENT = 0x0184;

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

        public VSListBox()
        {
            SetStyle(ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer, true);
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            IntegralHeight = false;
            Font = Typography.DefaultFont;
            ItemHeight = 28;
            Padding = Spacing.P1;

            _scrollBar = new VSScrollBar
            {
                Orientation = Orientation.Vertical,
                Width = ScrollBarWidth,
                Minimum = 0,
                Maximum = 0,
                Value = 0,
                ViewportSize = 0,
                Visible = false
            };
            _scrollBar.Scroll += OnScrollBarValueChanged;
            Controls.Add(_scrollBar);

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme();
        }

        #endregion

        #region Theme

        private void ApplyTheme()
        {
            var theme = ThemeManager.Current;
            BackColor = theme.BgSecondary;
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

        #region Handle / Scroll Suppression

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeScrollBarSuppressor.HideVertical(Handle);
            BeginInvoke(new Action(UpdateScroll));
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeScrollBarSuppressor.WM_NCCALCSIZE)
                NativeScrollBarSuppressor.HideVertical(Handle);

            base.WndProc(ref m);

            int reserve = (_scrollBar != null && _scrollBar.Visible) ? ScrollBarWidth + ScrollBarRightInset : 0;
            NativeScrollBarSuppressor.AdjustNcCalcSize(ref m, reserve);

            switch (m.Msg)
            {
                case LB_ADDSTRING:
                case LB_INSERTSTRING:
                case LB_DELETESTRING:
                case LB_RESETCONTENT:
                case NativeScrollBarSuppressor.WM_VSCROLL:
                    NativeScrollBarSuppressor.HideVertical(Handle);
                    UpdateScroll();
                    break;
                case NativeScrollBarSuppressor.WM_NCPAINT:
                case NativeScrollBarSuppressor.WM_PAINT:
                    NativeScrollBarSuppressor.HideVertical(Handle);
                    break;
            }
        }

        #endregion

        #region Scroll Sync

        private void OnScrollBarValueChanged(object sender, EventArgs e)
        {
            if (_syncingScroll) return;
            _syncingScroll = true;
            try
            {
                if (_scrollBar.Value != TopIndex)
                    TopIndex = _scrollBar.Value;
            }
            finally
            {
                _syncingScroll = false;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutScrollBar();
            UpdateScroll();
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            UpdateScroll();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_scrollBar.Visible)
            {
                if (_syncingScroll) return;
                _syncingScroll = true;
                try { _scrollBar.Value = TopIndex; }
                finally { _syncingScroll = false; }
            }
        }

        private void LayoutScrollBar()
        {
            if (_scrollBar == null) return;
            int inset = 2;
            _scrollBar.SetBounds(
                Width - ScrollBarWidth - ScrollBarRightInset,
                inset,
                ScrollBarWidth,
                Math.Max(0, Height - inset * 2));
        }

        private int GetVisibleItemCount()
        {
            int itemH = Math.Max(1, ItemHeight);
            return Math.Max(1, ClientSize.Height / itemH);
        }

        private void UpdateScroll()
        {
            if (_scrollBar == null) return;

            int total = Items.Count;
            int visible = GetVisibleItemCount();

            if (total <= visible)
            {
                if (_scrollBar.Visible)
                {
                    _scrollBar.Visible = false;
                    NativeScrollBarSuppressor.RecalcFrame(Handle);
                }
                return;
            }

            if (!_scrollBar.Visible)
            {
                _scrollBar.Visible = true;
                LayoutScrollBar();
                NativeScrollBarSuppressor.RecalcFrame(Handle);
            }

            _scrollBar.Maximum = total;
            _scrollBar.ViewportSize = visible;

            if (_syncingScroll) return;
            _syncingScroll = true;
            try { _scrollBar.Value = TopIndex; }
            finally { _syncingScroll = false; }

            _scrollBar.BringToFront();
        }

        #endregion

        #region Paint

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var theme = ThemeManager.Current;
            var g = e.Graphics;
            DesignRenderer.SetHighQuality(g);

            var bounds = e.Bounds;
            var isSelected = (e.State & DrawItemState.Selected) != 0;
            var isHovered = bounds.Contains(PointToClient(Cursor.Position));

            var insetBounds = new Rectangle(bounds.X + 4, bounds.Y + 1, Math.Max(0, bounds.Width - 8), bounds.Height - 2);
            Color bgColor;

            if (isSelected)
                bgColor = theme.AccentPrimaryMuted;
            else if (isHovered)
                bgColor = theme.BgTertiary;
            else
                bgColor = theme.BgSecondary;

            DesignRenderer.FillRoundedRect(g, insetBounds, bgColor, Effects.Rounded);

            var fgColor = isSelected ? theme.AccentPrimary : theme.TextPrimary;
            var textRect = new Rectangle(insetBounds.X + 8, insetBounds.Y, Math.Max(0, insetBounds.Width - 8), insetBounds.Height);
            DesignRenderer.DrawLeftText(g, Items[e.Index].ToString(), Font, fgColor, textRect);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            DesignRenderer.SetHighQuality(g);

            var theme = ThemeManager.Current;
            var bounds = ClientRectangle;

            DesignRenderer.FillRoundedRect(g, bounds, theme.BgSecondary, _cornerRadius);
            DesignRenderer.DrawRoundedRect(g, bounds, theme.BorderDefault, _cornerRadius);
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
