using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// WPF.Design ProgressBar.xaml에 대응하는 커스텀 프로그레스바.
    /// 둥근 모서리, AccentPrimary 인디케이터, 배경 트랙을 지원합니다.
    /// </summary>
    public class VSProgressBar : VSControlBase
    {
        #region Fields

        private int _value;
        private int _minimum;
        private int _maximum = 100;
        private int _cornerRadius = Effects.RoundedSm;

        #endregion

        #region Properties

        /// <summary>현재 값.</summary>
        [Category("Behavior")]
        [DefaultValue(0)]
        public int Value
        {
            get => _value;
            set
            {
                _value = Math.Max(_minimum, Math.Min(_maximum, value));
                Invalidate();
            }
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

        /// <summary>모서리 반경.</summary>
        [Category("Appearance")]
        [DefaultValue(2)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; Invalidate(); }
        }

        #endregion

        #region Constructor

        public VSProgressBar()
        {
            Size = new Size(200, Sizing.H1_5);
        }

        #endregion

        #region Paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            DesignRenderer.SetHighQuality(g);

            var theme = Theme;
            var bounds = ClientRectangle;

            // 배경 트랙
            DesignRenderer.FillRoundedRect(g, bounds, theme.BgTertiary, _cornerRadius);

            // 인디케이터
            if (_maximum > _minimum && _value > _minimum)
            {
                float ratio = (float)(_value - _minimum) / (_maximum - _minimum);
                int indicatorWidth = Math.Max((int)(bounds.Width * ratio), _cornerRadius * 2);
                var indicatorRect = new Rectangle(bounds.X, bounds.Y, indicatorWidth, bounds.Height);

                DesignRenderer.FillRoundedRect(g, indicatorRect, theme.AccentPrimary, _cornerRadius);
            }
        }

        #endregion
    }
}
