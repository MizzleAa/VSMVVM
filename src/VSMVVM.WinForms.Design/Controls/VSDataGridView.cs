using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// WPF.Design DataGrid.xaml에 대응하는 커스텀 DataGridView.
    /// 테마 색상, 셀 스타일, 헤더 스타일을 자동 적용합니다.
    /// 네이티브 세로 스크롤바를 구조적으로 억제하고 VSScrollBar 오버레이를 사용합니다.
    /// 컬럼 폭은 AutoSizeColumnsMode.None + FillWeight 비례 배분으로 직접 관리합니다.
    /// </summary>
    public class VSDataGridView : DataGridView
    {
        #region Fields

        private VSScrollBar _scrollBar;
        private bool _syncingScroll;
        private bool _suspendColumnWidthUpdate;

        private const int ScrollBarWidth = 8;
        private const int ScrollBarRightInset = 2;

        #endregion

        #region Constructor

        public VSDataGridView()
        {
            SetStyle(ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer, true);

            BorderStyle = BorderStyle.None;
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            RowHeadersVisible = false;
            EnableHeadersVisualStyles = false;
            SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            AllowUserToAddRows = false;
            AllowUserToResizeRows = false;
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            ScrollBars = ScrollBars.None;
            RowTemplate.Height = 36;
            ColumnHeadersHeight = 40;
            Font = Typography.DefaultFont;

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

            BackgroundColor = theme.BgSecondary;
            GridColor = theme.BorderMuted;

            DefaultCellStyle.BackColor = theme.BgSecondary;
            DefaultCellStyle.ForeColor = theme.TextPrimary;
            DefaultCellStyle.SelectionBackColor = theme.AccentPrimaryMuted;
            DefaultCellStyle.SelectionForeColor = theme.AccentPrimary;
            DefaultCellStyle.Font = Typography.DefaultFont;
            DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);

            ColumnHeadersDefaultCellStyle.BackColor = theme.BgTertiary;
            ColumnHeadersDefaultCellStyle.ForeColor = theme.TextSecondary;
            ColumnHeadersDefaultCellStyle.Font = Typography.MediumFont;
            ColumnHeadersDefaultCellStyle.SelectionBackColor = theme.BgTertiary;
            ColumnHeadersDefaultCellStyle.SelectionForeColor = theme.TextSecondary;
            ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 4, 8, 4);

            AlternatingRowsDefaultCellStyle.BackColor = theme.BgPrimary;

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
            BeginInvoke(new Action(() =>
            {
                UpdateColumnWidths();
                UpdateScroll();
            }));
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

        protected override void OnRowsAdded(DataGridViewRowsAddedEventArgs e)
        {
            base.OnRowsAdded(e);
            NativeScrollBarSuppressor.HideVertical(Handle);
            UpdateScroll();
        }

        protected override void OnRowsRemoved(DataGridViewRowsRemovedEventArgs e)
        {
            base.OnRowsRemoved(e);
            UpdateScroll();
        }

        protected override void OnColumnAdded(DataGridViewColumnEventArgs e)
        {
            base.OnColumnAdded(e);
            UpdateColumnWidths();
        }

        protected override void OnColumnRemoved(DataGridViewColumnEventArgs e)
        {
            base.OnColumnRemoved(e);
            UpdateColumnWidths();
        }

        #endregion

        #region Column Widths

        /// <summary>
        /// AutoSizeColumnsMode.None 상태에서 FillWeight 비율로 컬럼 폭을 비례 배분한다.
        /// ClientSize.Width 에서 스크롤바 예약 폭을 뺀 값을 총 폭으로 사용.
        /// </summary>
        private void UpdateColumnWidths()
        {
            if (_suspendColumnWidthUpdate) return;
            if (ColumnCount == 0) return;

            int reserve = (_scrollBar != null && _scrollBar.Visible) ? ScrollBarWidth + ScrollBarRightInset : 0;
            int avail = Math.Max(0, ClientSize.Width - reserve);
            if (avail <= 0) return;

            float totalWeight = 0f;
            for (int i = 0; i < ColumnCount; i++)
                totalWeight += Columns[i].FillWeight;
            if (totalWeight <= 0) return;

            _suspendColumnWidthUpdate = true;
            try
            {
                int used = 0;
                for (int i = 0; i < ColumnCount - 1; i++)
                {
                    int w = (int)(avail * Columns[i].FillWeight / totalWeight);
                    w = Math.Max(10, w);
                    Columns[i].Width = w;
                    used += w;
                }
                Columns[ColumnCount - 1].Width = Math.Max(10, avail - used);
            }
            finally
            {
                _suspendColumnWidthUpdate = false;
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
                if (RowCount > 0 && _scrollBar.Value != FirstDisplayedScrollingRowIndex)
                {
                    int target = Math.Max(0, Math.Min(RowCount - 1, _scrollBar.Value));
                    FirstDisplayedScrollingRowIndex = target;
                }
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
            UpdateColumnWidths();
        }

        protected override void OnScroll(ScrollEventArgs e)
        {
            base.OnScroll(e);
            if (_syncingScroll) return;
            _syncingScroll = true;
            try { _scrollBar.Value = FirstDisplayedScrollingRowIndex < 0 ? 0 : FirstDisplayedScrollingRowIndex; }
            finally { _syncingScroll = false; }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_scrollBar.Visible)
            {
                if (_syncingScroll) return;
                _syncingScroll = true;
                try { _scrollBar.Value = FirstDisplayedScrollingRowIndex < 0 ? 0 : FirstDisplayedScrollingRowIndex; }
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

        private int GetVisibleRowCount()
        {
            int rowH = Math.Max(1, RowTemplate.Height);
            int avail = ClientSize.Height - ColumnHeadersHeight;
            return Math.Max(1, avail / rowH);
        }

        private void UpdateScroll()
        {
            if (_scrollBar == null) return;

            int total = RowCount;
            int visible = GetVisibleRowCount();
            bool prevVisible = _scrollBar.Visible;

            if (total <= visible)
            {
                if (_scrollBar.Visible)
                {
                    _scrollBar.Visible = false;
                    NativeScrollBarSuppressor.RecalcFrame(Handle);
                    UpdateColumnWidths();
                }
                return;
            }

            if (!_scrollBar.Visible)
            {
                _scrollBar.Visible = true;
                LayoutScrollBar();
                NativeScrollBarSuppressor.RecalcFrame(Handle);
                UpdateColumnWidths();
            }

            _scrollBar.Maximum = total;
            _scrollBar.ViewportSize = visible;

            if (_syncingScroll) return;
            _syncingScroll = true;
            try { _scrollBar.Value = FirstDisplayedScrollingRowIndex < 0 ? 0 : FirstDisplayedScrollingRowIndex; }
            finally { _syncingScroll = false; }

            _scrollBar.BringToFront();
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
