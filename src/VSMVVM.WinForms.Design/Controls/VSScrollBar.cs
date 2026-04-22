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
    /// WPF.Design ScrollViewer.xaml에 대응하는 커스텀 스크롤바.
    /// 얇은 둥근 트랙, 호버 시 thumb 색상 변경을 지원합니다.
    /// 표준 ScrollBar를 대체하는 독립 컨트롤입니다.
    /// </summary>
    public class VSScrollBar : VSControlBase
    {
        #region Fields

        private Orientation _orientation = Orientation.Vertical;
        private int _minimum;
        private int _maximum = 100;
        private int _value;
        private int _viewportSize = 10;
        private bool _thumbHovered;
        private bool _thumbDragging;
        private int _dragOffset;

        #endregion

        #region Properties

        /// <summary>방향.</summary>
        [Category("Behavior")]
        [DefaultValue(typeof(Orientation), "Vertical")]
        public Orientation Orientation
        {
            get => _orientation;
            set { _orientation = value; Invalidate(); }
        }

        /// <summary>최소값.</summary>
        [Category("Behavior")]
        [DefaultValue(0)]
        public int Minimum
        {
            get => _minimum;
            set { _minimum = value; Invalidate(); }
        }

        /// <summary>최대값.</summary>
        [Category("Behavior")]
        [DefaultValue(100)]
        public int Maximum
        {
            get => _maximum;
            set { _maximum = value; Invalidate(); }
        }

        /// <summary>현재 값.</summary>
        [Category("Behavior")]
        [DefaultValue(0)]
        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Max(_minimum, Math.Min(_maximum - _viewportSize, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    Scroll?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        /// <summary>뷰포트 크기 (thumb 크기 결정).</summary>
        [Category("Behavior")]
        [DefaultValue(10)]
        public int ViewportSize
        {
            get => _viewportSize;
            set { _viewportSize = Math.Max(1, value); Invalidate(); }
        }

        #endregion

        #region Events

        /// <summary>스크롤 이벤트.</summary>
        public event EventHandler Scroll;

        #endregion

        #region Constructor

        public VSScrollBar()
        {
            Size = _orientation == Orientation.Vertical
                ? new Size(Sizing.W2, 200)
                : new Size(200, Sizing.H2);
        }

        #endregion

        #region Paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            DesignRenderer.SetHighQuality(g);

            var theme = Theme;
            var bounds = ClientRectangle;

            // 트랙 배경
            DesignRenderer.FillRoundedRect(g, bounds, theme.ScrollbarTrack, Effects.Rounded);

            // Thumb
            var thumbRect = GetThumbRect();
            if (thumbRect.Width > 0 && thumbRect.Height > 0)
            {
                var thumbColor = _thumbHovered || _thumbDragging
                    ? theme.ScrollbarThumbHover
                    : theme.ScrollbarThumb;
                DesignRenderer.FillRoundedRect(g, thumbRect, thumbColor, Effects.Rounded);
            }
        }

        #endregion

        #region Thumb Calculation

        private Rectangle GetThumbRect()
        {
            int range = _maximum - _minimum;
            if (range <= 0) return Rectangle.Empty;

            if (_orientation == Orientation.Vertical)
            {
                int trackHeight = Height;
                float thumbRatio = Math.Min(1f, (float)_viewportSize / range);
                int thumbHeight = Math.Max(20, (int)(trackHeight * thumbRatio));
                int usableTrack = trackHeight - thumbHeight;
                float valueRatio = range - _viewportSize > 0
                    ? (float)(_value - _minimum) / (range - _viewportSize)
                    : 0;
                int thumbY = (int)(usableTrack * valueRatio);

                return new Rectangle(0, thumbY, Width, thumbHeight);
            }
            else
            {
                int trackWidth = Width;
                float thumbRatio = Math.Min(1f, (float)_viewportSize / range);
                int thumbWidth = Math.Max(20, (int)(trackWidth * thumbRatio));
                int usableTrack = trackWidth - thumbWidth;
                float valueRatio = range - _viewportSize > 0
                    ? (float)(_value - _minimum) / (range - _viewportSize)
                    : 0;
                int thumbX = (int)(usableTrack * valueRatio);

                return new Rectangle(thumbX, 0, thumbWidth, Height);
            }
        }

        #endregion

        #region Mouse Interaction

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_thumbDragging)
            {
                int range = _maximum - _minimum - _viewportSize;
                if (range <= 0) return;

                if (_orientation == Orientation.Vertical)
                {
                    int thumbHeight = GetThumbRect().Height;
                    int usableTrack = Height - thumbHeight;
                    if (usableTrack <= 0) return;
                    float ratio = (float)(e.Y - _dragOffset) / usableTrack;
                    Value = _minimum + (int)(ratio * range);
                }
                else
                {
                    int thumbWidth = GetThumbRect().Width;
                    int usableTrack = Width - thumbWidth;
                    if (usableTrack <= 0) return;
                    float ratio = (float)(e.X - _dragOffset) / usableTrack;
                    Value = _minimum + (int)(ratio * range);
                }
            }
            else
            {
                bool oldHovered = _thumbHovered;
                _thumbHovered = GetThumbRect().Contains(e.Location);
                if (oldHovered != _thumbHovered) Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            var thumbRect = GetThumbRect();
            if (thumbRect.Contains(e.Location))
            {
                _thumbDragging = true;
                _dragOffset = _orientation == Orientation.Vertical
                    ? e.Y - thumbRect.Y
                    : e.X - thumbRect.X;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _thumbDragging = false;
            base.OnMouseUp(e);
        }

        #endregion
    }
}
