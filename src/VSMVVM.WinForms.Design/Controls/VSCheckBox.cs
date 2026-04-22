using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// WPF.Design CheckBox.xaml에 대응하는 커스텀 체크박스.
    /// 둥근 모서리 체크 박스 + 체크마크 커스텀 드로잉.
    /// </summary>
    [DefaultEvent("CheckedChanged")]
    public class VSCheckBox : VSControlBase
    {
        #region Fields

        private bool _checked;
        private int _boxSize = Sizing.H4_5;

        #endregion

        #region Properties

        /// <summary>체크 상태.</summary>
        [Category("Appearance")]
        [DefaultValue(false)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        /// <summary>체크 박스 크기 (px).</summary>
        [Category("Appearance")]
        [DefaultValue(18)]
        public int BoxSize
        {
            get => _boxSize;
            set { _boxSize = value; Invalidate(); }
        }

        #endregion

        #region Events

        /// <summary>체크 상태 변경 이벤트.</summary>
        public event EventHandler CheckedChanged;

        #endregion

        #region Constructor

        public VSCheckBox()
        {
            Font = Typography.DefaultFont;
            Cursor = Cursors.Hand;
            Size = new Size(150, 24);
        }

        #endregion

        #region Paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            DesignRenderer.SetHighQuality(g);

            var theme = Theme;
            int y = (Height - _boxSize) / 2;
            var boxRect = new Rectangle(0, y, _boxSize, _boxSize);

            // 체크 박스 배경
            var bgColor = _checked ? theme.AccentPrimary : theme.BgTertiary;
            DesignRenderer.FillRoundedRect(g, boxRect, bgColor, Effects.Rounded);

            // 체크 박스 테두리
            var borderColor = _checked ? theme.AccentPrimary :
                              IsHovered ? theme.BorderHover : theme.BorderDefault;
            DesignRenderer.DrawRoundedRect(g, boxRect, borderColor, Effects.Rounded);

            // 체크마크
            if (_checked)
            {
                var markRect = new Rectangle(boxRect.X + 3, boxRect.Y + 3, boxRect.Width - 6, boxRect.Height - 6);
                DesignRenderer.DrawCheckMark(g, markRect, theme.TextInverse, 2f);
            }

            // 레이블 텍스트
            if (!string.IsNullOrEmpty(Text))
            {
                var textRect = new Rectangle(
                    _boxSize + 8, 0, Width - _boxSize - 8, Height);
                DesignRenderer.DrawLeftText(g, Text, Font, theme.TextPrimary, textRect);
            }

            // Disabled
            if (!Enabled)
            {
                using var overlay = new SolidBrush(Color.FromArgb(128, theme.BgPrimary));
                g.FillRectangle(overlay, ClientRectangle);
            }
        }

        #endregion

        #region Click

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        #endregion
    }

    /// <summary>
    /// WPF.Design CheckBox.xaml의 RadioButton에 대응하는 커스텀 라디오 버튼.
    /// 원형 체크 박스 + 내부 도트 커스텀 드로잉.
    /// </summary>
    [DefaultEvent("CheckedChanged")]
    public class VSRadioButton : VSControlBase
    {
        #region Fields

        private bool _checked;
        private int _boxSize = Sizing.H4_5;

        #endregion

        #region Properties

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    if (_checked) UncheckSiblings();
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(18)]
        public int BoxSize
        {
            get => _boxSize;
            set { _boxSize = value; Invalidate(); }
        }

        #endregion

        #region Events

        public event EventHandler CheckedChanged;

        #endregion

        #region Constructor

        public VSRadioButton()
        {
            Font = Typography.DefaultFont;
            Cursor = Cursors.Hand;
            Size = new Size(150, 24);
        }

        #endregion

        #region Paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            DesignRenderer.SetHighQuality(g);

            var theme = Theme;
            int y = (Height - _boxSize) / 2;
            var boxRect = new Rectangle(0, y, _boxSize, _boxSize);

            // 원형 배경
            var bgColor = _checked ? theme.AccentPrimary : theme.BgTertiary;
            using var bgBrush = new SolidBrush(bgColor);
            g.FillEllipse(bgBrush, boxRect);

            // 원형 테두리
            var borderColor = _checked ? theme.AccentPrimary :
                              IsHovered ? theme.BorderHover : theme.BorderDefault;
            using var borderPen = new Pen(borderColor, Effects.Border);
            g.DrawEllipse(borderPen, boxRect);

            // 내부 도트
            if (_checked)
            {
                DesignRenderer.FillRadioDot(g, boxRect, theme.TextInverse, Sizing.W2);
            }

            // 레이블 텍스트
            if (!string.IsNullOrEmpty(Text))
            {
                var textRect = new Rectangle(
                    _boxSize + 8, 0, Width - _boxSize - 8, Height);
                DesignRenderer.DrawLeftText(g, Text, Font, theme.TextPrimary, textRect);
            }
        }

        #endregion

        #region Click

        protected override void OnClick(EventArgs e)
        {
            if (!_checked) Checked = true;
            base.OnClick(e);
        }

        private void UncheckSiblings()
        {
            if (Parent == null) return;
            foreach (Control sibling in Parent.Controls)
            {
                if (sibling is VSRadioButton radio && radio != this)
                {
                    radio.Checked = false;
                }
            }
        }

        #endregion
    }
}
