using System;
using System.Drawing;
using System.Windows.Forms;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// 자식 컨트롤(주로 FlowLayoutPanel) 하나를 수직 스크롤 가능한 뷰포트로 감싸고,
    /// 오른쪽 가장자리에 <see cref="VSScrollBar"/>를 오버레이하는 컨테이너.
    /// OS 기본 스크롤바를 숨기고 일관된 디자인 토큰 스타일을 적용하기 위한 래퍼.
    /// </summary>
    public class VSScrollHost : Panel
    {
        private readonly VSScrollBar _scrollBar;
        private Control _content;
        private bool _syncing;

        private const int ScrollBarWidth = 8;
        private const int ScrollBarRightInset = 2;
        private const int WheelStep = 40;

        public VSScrollHost()
        {
            _scrollBar = new VSScrollBar
            {
                Orientation = Orientation.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
                Width = ScrollBarWidth,
                Minimum = 0,
                Maximum = 0,
                Value = 0,
                ViewportSize = 0
            };
            _scrollBar.Scroll += OnScrollBarValueChanged;
            Controls.Add(_scrollBar);
        }

        /// <summary>스크롤 대상 자식 컨트롤(한 개). 일반적으로 FlowLayoutPanel.</summary>
        public Control Content
        {
            get => _content;
            set
            {
                if (_content == value) return;
                if (_content != null)
                {
                    _content.SizeChanged -= OnContentMetricsChanged;
                    _content.ControlAdded -= OnContentChildrenChanged;
                    _content.ControlRemoved -= OnContentChildrenChanged;
                    _content.MouseWheel -= OnContentMouseWheel;
                    Controls.Remove(_content);
                }
                _content = value;
                if (_content != null)
                {
                    _content.Location = Point.Empty;
                    _content.SizeChanged += OnContentMetricsChanged;
                    _content.ControlAdded += OnContentChildrenChanged;
                    _content.ControlRemoved += OnContentChildrenChanged;
                    _content.MouseWheel += OnContentMouseWheel;
                    Controls.Add(_content);
                    _content.SendToBack();
                    _scrollBar.BringToFront();
                }
                UpdateScroll();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutChildren();
            UpdateScroll();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ApplyWheel(e.Delta);
        }

        private void OnContentMouseWheel(object sender, MouseEventArgs e)
        {
            ApplyWheel(e.Delta);
            if (e is HandledMouseEventArgs h) h.Handled = true;
        }

        private void ApplyWheel(int delta)
        {
            if (_scrollBar.Maximum <= _scrollBar.ViewportSize) return;
            int step = delta > 0 ? -WheelStep : WheelStep;
            _scrollBar.Value = _scrollBar.Value + step;
        }

        private void OnContentMetricsChanged(object sender, EventArgs e) => UpdateScroll();
        private void OnContentChildrenChanged(object sender, ControlEventArgs e) => UpdateScroll();

        private void OnScrollBarValueChanged(object sender, EventArgs e)
        {
            if (_syncing || _content == null) return;
            _syncing = true;
            try
            {
                _content.Top = -_scrollBar.Value;
            }
            finally
            {
                _syncing = false;
            }
        }

        private void LayoutChildren()
        {
            _scrollBar.Location = new Point(
                Width - ScrollBarWidth - ScrollBarRightInset,
                0);
            _scrollBar.Height = Height;

            if (_content != null)
            {
                _content.Width = Width - ScrollBarWidth - ScrollBarRightInset;
            }
        }

        private void UpdateScroll()
        {
            if (_content == null) return;

            int contentHeight = _content.Height;
            int viewportHeight = Height;

            if (contentHeight <= viewportHeight)
            {
                _scrollBar.Visible = false;
                _content.Top = 0;
                _scrollBar.Maximum = 0;
                _scrollBar.ViewportSize = 0;
                _scrollBar.Value = 0;
                return;
            }

            _scrollBar.Visible = true;
            _scrollBar.Maximum = contentHeight;
            _scrollBar.ViewportSize = viewportHeight;
            int maxOffset = contentHeight - viewportHeight;
            if (-_content.Top > maxOffset)
                _content.Top = -maxOffset;
            _scrollBar.Value = -_content.Top;
        }
    }
}
