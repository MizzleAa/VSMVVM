using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// WPF.Design TextBox.xaml에 대응하는 커스텀 텍스트박스.
    /// 둥근 모서리 Border, Focus/Hover 상태 테두리 색상 변경을 지원합니다.
    /// Multiline=true일 때 네이티브 세로 스크롤바 대신 VSScrollBar 오버레이를 사용합니다.
    /// </summary>
    public class VSTextBox : UserControl
    {
        #region Fields

        private readonly TextBoxNoScroll _innerTextBox;
        private readonly VSScrollBar _scrollBar;
        private int _cornerRadius = Effects.RoundedMd;
        private bool _isHovered;
        private bool _isFocused;
        private bool _syncingScroll;
        private ThemeSubscription _themeSub;

        private const int ScrollBarWidth = 8;
        private const int ScrollBarRightInset = 2;
        private const int EM_LINESCROLL = 0x00B6;
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Properties

        /// <summary>텍스트 값.</summary>
        [Browsable(true)]
        [Category("Appearance")]
        public override string Text
        {
            get => _innerTextBox.Text;
            set => _innerTextBox.Text = value;
        }

        /// <summary>모서리 반경.</summary>
        [Category("Appearance")]
        [DefaultValue(6)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; DesignRenderer.ApplyRoundedRegion(this, _cornerRadius); Invalidate(); }
        }

        /// <summary>읽기 전용 여부.</summary>
        [Category("Behavior")]
        public bool ReadOnly
        {
            get => _innerTextBox.ReadOnly;
            set => _innerTextBox.ReadOnly = value;
        }

        /// <summary>비밀번호 문자.</summary>
        [Category("Behavior")]
        public char PasswordChar
        {
            get => _innerTextBox.PasswordChar;
            set => _innerTextBox.PasswordChar = value;
        }

        /// <summary>PlaceholderText.</summary>
        [Category("Appearance")]
        public string PlaceholderText
        {
            get => _innerTextBox.PlaceholderText;
            set => _innerTextBox.PlaceholderText = value;
        }

        /// <summary>여러 줄 입력 여부.</summary>
        [Category("Behavior")]
        [DefaultValue(false)]
        public bool Multiline
        {
            get => _innerTextBox.Multiline;
            set
            {
                if (_innerTextBox.Multiline == value) return;
                _innerTextBox.Multiline = value;
                _innerTextBox.WordWrap = value;
                UpdateInnerTextBoxBounds();
                UpdateScroll();
            }
        }

        /// <summary>텍스트 줄 배열.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string[] Lines
        {
            get => _innerTextBox.Lines;
            set => _innerTextBox.Lines = value;
        }

        #endregion

        #region Events

        /// <summary>텍스트 변경 이벤트.</summary>
        [Browsable(true)]
        public new event EventHandler TextChanged
        {
            add => _innerTextBox.TextChanged += value;
            remove => _innerTextBox.TextChanged -= value;
        }

        #endregion

        #region Constructor

        public VSTextBox()
        {
            ControlStyleHelper.ApplyVSDefaultStyles(this, supportTransparent: false);

            _innerTextBox = new TextBoxNoScroll
            {
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.None
            };

            _innerTextBox.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _innerTextBox.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };
            _innerTextBox.TextChanged += OnInnerTextChanged;
            _innerTextBox.Resize += (s, e) => UpdateScroll();
            _innerTextBox.MouseWheel += OnInnerMouseWheel;
            _innerTextBox.VScrolled += (s, e) => SyncFromTextBox();

            Controls.Add(_innerTextBox);

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

            Font = Typography.DefaultFont;
            Size = new Size(200, 32);
            Padding = Spacing.Px2Py1;

            _themeSub = new ThemeSubscription(this, ApplyTheme);
            ApplyTheme();
            DesignRenderer.ApplyRoundedRegion(this, _cornerRadius);
        }

        #endregion

        #region Theme

        private void ApplyTheme()
        {
            var theme = ThemeManager.Current;
            _innerTextBox.BackColor = theme.BgTertiary;
            _innerTextBox.ForeColor = theme.TextPrimary;
            BackColor = theme.BgTertiary;
            Invalidate();
        }

        #endregion

        #region Layout

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateInnerTextBoxBounds();
            DesignRenderer.ApplyRoundedRegion(this, _cornerRadius);
            UpdateScroll();
        }

        private void UpdateInnerTextBoxBounds()
        {
            int inset = _cornerRadius / 2 + 4;

            if (_innerTextBox.Multiline)
            {
                int sbReserve = _scrollBar != null && _scrollBar.Visible ? ScrollBarWidth + ScrollBarRightInset : 0;
                int x = inset;
                int y = inset;
                int w = Math.Max(0, Width - inset * 2 - sbReserve);
                int h = Math.Max(0, Height - inset * 2);
                _innerTextBox.SetBounds(x, y, w, h);

                if (_innerTextBox.ReserveRight != sbReserve)
                {
                    _innerTextBox.ReserveRight = sbReserve;
                    if (_innerTextBox.IsHandleCreated)
                        NativeScrollBarSuppressor.RecalcFrame(_innerTextBox.Handle);
                }

                if (_scrollBar != null)
                {
                    _scrollBar.SetBounds(
                        Width - ScrollBarWidth - ScrollBarRightInset,
                        inset,
                        ScrollBarWidth,
                        Math.Max(0, Height - inset * 2));
                }
            }
            else
            {
                int y = (Height - _innerTextBox.PreferredHeight) / 2;
                _innerTextBox.SetBounds(inset, y, Math.Max(0, Width - inset * 2), _innerTextBox.PreferredHeight);
                if (_innerTextBox.ReserveRight != 0)
                {
                    _innerTextBox.ReserveRight = 0;
                    if (_innerTextBox.IsHandleCreated)
                        NativeScrollBarSuppressor.RecalcFrame(_innerTextBox.Handle);
                }
                if (_scrollBar != null) _scrollBar.Visible = false;
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            UpdateInnerTextBoxBounds();
        }

        #endregion

        #region Scroll Sync

        private void OnInnerTextChanged(object sender, EventArgs e)
        {
            if (_innerTextBox.Multiline) UpdateScroll();
        }

        private void OnInnerMouseWheel(object sender, MouseEventArgs e)
        {
            if (!_innerTextBox.Multiline) return;
            if (!_scrollBar.Visible) return;
            int step = e.Delta > 0 ? -1 : 1;
            _scrollBar.Value = _scrollBar.Value + step * 3;
            if (e is HandledMouseEventArgs h) h.Handled = true;
        }

        private void OnScrollBarValueChanged(object sender, EventArgs e)
        {
            if (_syncingScroll) return;
            _syncingScroll = true;
            try
            {
                int currentFirst = GetFirstVisibleLine();
                int delta = _scrollBar.Value - currentFirst;
                if (delta != 0)
                    SendMessage(_innerTextBox.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
            }
            finally
            {
                _syncingScroll = false;
            }
        }

        private void SyncFromTextBox()
        {
            if (_syncingScroll) return;
            if (!_innerTextBox.Multiline || !_scrollBar.Visible) return;
            _syncingScroll = true;
            try
            {
                _scrollBar.Value = GetFirstVisibleLine();
            }
            finally
            {
                _syncingScroll = false;
            }
        }

        private int GetFirstVisibleLine()
        {
            if (!IsHandleCreated || !_innerTextBox.IsHandleCreated) return 0;
            return (int)SendMessage(_innerTextBox.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
        }

        private int GetVisibleLineCount()
        {
            int lineH = Math.Max(1, _innerTextBox.Font.Height);
            return Math.Max(1, _innerTextBox.ClientSize.Height / lineH);
        }

        private void UpdateScroll()
        {
            if (_scrollBar == null) return;
            if (!_innerTextBox.Multiline)
            {
                _scrollBar.Visible = false;
                return;
            }

            int totalLines = _innerTextBox.Lines?.Length ?? 0;
            int visibleLines = GetVisibleLineCount();

            if (totalLines <= visibleLines)
            {
                if (_scrollBar.Visible)
                {
                    _scrollBar.Visible = false;
                    UpdateInnerTextBoxBounds();
                }
                return;
            }

            if (!_scrollBar.Visible)
            {
                _scrollBar.Visible = true;
                UpdateInnerTextBoxBounds();
            }

            _scrollBar.Maximum = totalLines;
            _scrollBar.ViewportSize = visibleLines;
            _scrollBar.Value = GetFirstVisibleLine();
            _scrollBar.BringToFront();
        }

        #endregion

        #region Paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            DesignRenderer.SetHighQuality(g);

            var theme = ThemeManager.Current;
            var bounds = ClientRectangle;

            DesignRenderer.FillRoundedRect(g, bounds, theme.BgTertiary, _cornerRadius);

            var borderColor = _isFocused ? theme.BorderFocus :
                              _isHovered ? theme.BorderHover :
                              theme.BorderDefault;

            DesignRenderer.DrawRoundedRect(g, bounds, borderColor, _cornerRadius);

            if (!Enabled)
            {
                using var overlay = new SolidBrush(Color.FromArgb(128, theme.BgPrimary));
                using var path = DesignRenderer.CreateRoundedRect(bounds, _cornerRadius);
                g.FillPath(overlay, path);
            }
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

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            OnInnerMouseWheel(this, e);
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

        /// <summary>
        /// 네이티브 스크롤바를 구조적으로 억제하고 WM_VSCROLL을 이벤트로 노출하는 내부 TextBox.
        /// WM_NCCALCSIZE 단계에서 클라이언트 영역의 오른쪽을 <see cref="ReserveRight"/> 만큼
        /// 축소하여 OS가 스크롤바 공간을 할당하지 못하도록 만든다.
        /// </summary>
        private class TextBoxNoScroll : TextBox
        {
            public event EventHandler VScrolled;

            /// <summary>클라이언트 영역 오른쪽에서 예약할 픽셀 수(스크롤바 overlay 공간).</summary>
            public int ReserveRight { get; set; }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeScrollBarSuppressor.WM_NCCALCSIZE)
                    NativeScrollBarSuppressor.HideVertical(Handle);

                base.WndProc(ref m);

                NativeScrollBarSuppressor.AdjustNcCalcSize(ref m, ReserveRight);

                switch (m.Msg)
                {
                    case NativeScrollBarSuppressor.WM_VSCROLL:
                        NativeScrollBarSuppressor.HideVertical(Handle);
                        VScrolled?.Invoke(this, EventArgs.Empty);
                        break;
                    case NativeScrollBarSuppressor.WM_NCPAINT:
                    case NativeScrollBarSuppressor.WM_PAINT:
                        NativeScrollBarSuppressor.HideVertical(Handle);
                        break;
                }
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                NativeScrollBarSuppressor.HideVertical(Handle);
            }
        }
    }
}
