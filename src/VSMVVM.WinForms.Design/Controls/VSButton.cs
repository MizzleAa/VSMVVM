using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Input;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// 버튼 스타일 Variant. WPF.Design Button.xaml의 Named Styles에 대응합니다.
    /// </summary>
    public enum ButtonVariant
    {
        /// <summary>AccentPrimary 배경, TextInverse 전경 (기본)</summary>
        Primary,
        /// <summary>BgSecondary 배경, BorderDefault 테두리</summary>
        Secondary,
        /// <summary>투명 배경, AccentPrimary 테두리/전경</summary>
        Outline,
        /// <summary>투명 배경, TextPrimary 전경 (hover 시 배경)</summary>
        Ghost,
        /// <summary>Error 배경, TextInverse 전경</summary>
        Danger,
        /// <summary>Success 배경, TextInverse 전경</summary>
        Success
    }

    /// <summary>
    /// WPF.Design Button.xaml에 대응하는 커스텀 버튼 컨트롤.
    /// 둥근 모서리, 호버/프레스 상태, 6가지 Variant를 지원합니다.
    /// </summary>
    [DefaultEvent("Click")]
    public class VSButton : VSControlBase
    {
        #region Fields

        private ButtonVariant _variant = ButtonVariant.Primary;
        private int _cornerRadius = Effects.RoundedMd;
        private ContentAlignment _textAlign = ContentAlignment.MiddleCenter;
        private ICommand _command;
        private object _commandParameter;

        #endregion

        #region Properties

        /// <summary>버튼 스타일 variant.</summary>
        [Category("Appearance")]
        [DefaultValue(ButtonVariant.Primary)]
        public ButtonVariant Variant
        {
            get => _variant;
            set { _variant = value; Invalidate(); }
        }

        /// <summary>모서리 반경 (px).</summary>
        [Category("Appearance")]
        [DefaultValue(6)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; UpdateRoundedRegion(_cornerRadius); Invalidate(); }
        }

        /// <summary>텍스트 정렬. WPF NavButton의 HorizontalContentAlignment=Left 지원.</summary>
        [Category("Appearance")]
        [DefaultValue(ContentAlignment.MiddleCenter)]
        public ContentAlignment TextAlign
        {
            get => _textAlign;
            set { _textAlign = value; Invalidate(); }
        }

        /// <summary>
        /// 클릭 시 실행할 ICommand. ViewModel의 Command 속성을 OnLoad에서 할당합니다.
        /// CanExecute=false이면 Enabled가 자동으로 false가 됩니다.
        /// </summary>
        [Category("Behavior")]
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [DefaultValue(null)]
        public ICommand Command
        {
            get => _command;
            set
            {
                if (_command == value) return;
                if (_command != null) _command.CanExecuteChanged -= OnCommandCanExecuteChanged;
                _command = value;
                if (_command != null)
                {
                    _command.CanExecuteChanged += OnCommandCanExecuteChanged;
                    Enabled = _command.CanExecute(_commandParameter);
                }
            }
        }

        /// <summary>Command.Execute/CanExecute에 전달할 파라미터.</summary>
        [Category("Behavior")]
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [DefaultValue(null)]
        public object CommandParameter
        {
            get => _commandParameter;
            set
            {
                _commandParameter = value;
                if (_command != null) Enabled = _command.CanExecute(_commandParameter);
            }
        }

        #endregion

        #region Constructor

        public VSButton()
        {
            Padding = Spacing.Px3Py2;
            Font = Typography.MediumFont;
            Cursor = Cursors.Hand;
            Size = new Size(100, 36);
            UpdateRoundedRegion(_cornerRadius);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRoundedRegion(_cornerRadius);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (_command != null && _command.CanExecute(_commandParameter))
                _command.Execute(_commandParameter);
        }

        private void OnCommandCanExecuteChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke((Action)(() => Enabled = _command?.CanExecute(_commandParameter) ?? true)); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }
            else
            {
                Enabled = _command?.CanExecute(_commandParameter) ?? true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _command != null)
            {
                _command.CanExecuteChanged -= OnCommandCanExecuteChanged;
                _command = null;
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            DesignRenderer.SetHighQuality(g);

            var bounds = ClientRectangle;
            var theme = Theme;

            // 배경색, 전경색, 테두리색 결정
            ResolveColors(theme, out var bgColor, out var fgColor, out var borderColor);

            // Disabled 상태
            if (!Enabled)
            {
                bgColor = Color.FromArgb((int)(255 * Effects.Opacity50), bgColor);
                fgColor = Color.FromArgb((int)(255 * Effects.Opacity50), fgColor);
            }

            // 배경 그리기
            DesignRenderer.FillRoundedRect(g, bounds, bgColor, _cornerRadius);

            // 테두리 그리기
            if (borderColor != Color.Transparent)
            {
                DesignRenderer.DrawRoundedRect(g, bounds, borderColor, _cornerRadius);
            }

            // 텍스트 그리기
            var textBounds = new Rectangle(
                bounds.X + Padding.Left,
                bounds.Y + Padding.Top,
                bounds.Width - Padding.Horizontal,
                bounds.Height - Padding.Vertical);

            if (_textAlign == ContentAlignment.MiddleLeft || _textAlign == ContentAlignment.TopLeft || _textAlign == ContentAlignment.BottomLeft)
                DesignRenderer.DrawLeftText(g, Text, Font, fgColor, textBounds);
            else
                DesignRenderer.DrawCenteredText(g, Text, Font, fgColor, textBounds);
        }

        private void ResolveColors(Colors.ITheme theme, out Color bg, out Color fg, out Color border)
        {
            border = Color.Transparent;

            switch (_variant)
            {
                case ButtonVariant.Primary:
                    bg = IsPressed ? theme.AccentPrimaryActive : IsHovered ? theme.AccentPrimaryHover : theme.AccentPrimary;
                    fg = theme.TextInverse;
                    break;

                case ButtonVariant.Secondary:
                    bg = IsHovered ? theme.BgTertiary : theme.BgSecondary;
                    fg = theme.TextPrimary;
                    border = theme.BorderDefault;
                    break;

                case ButtonVariant.Outline:
                    bg = IsHovered ? theme.AccentPrimaryMuted : Color.Transparent;
                    fg = theme.AccentPrimary;
                    border = theme.AccentPrimary;
                    break;

                case ButtonVariant.Ghost:
                    bg = IsHovered ? theme.BgSecondary : Color.Transparent;
                    fg = theme.TextPrimary;
                    break;

                case ButtonVariant.Danger:
                    bg = IsHovered ? theme.ErrorHover : theme.Error;
                    fg = theme.TextInverse;
                    break;

                case ButtonVariant.Success:
                    bg = IsHovered ? theme.SuccessHover : theme.Success;
                    fg = theme.TextInverse;
                    break;

                default:
                    bg = theme.AccentPrimary;
                    fg = theme.TextInverse;
                    break;
            }
        }

        #endregion
    }
}
