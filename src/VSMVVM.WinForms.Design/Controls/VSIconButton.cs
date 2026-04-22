using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Input;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Tokens;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// WPF BtnIcon 스타일에 대응하는 아이콘 전용 버튼.
    /// SVG path를 AccentPrimary 색으로 Uniform 스케일 렌더, hover 시 BgTertiary 배경.
    /// </summary>
    public class VSIconButton : VSControlBase
    {
        #region Fields

        private SvgIcon _icon;
        private int _iconSize = 16;           // W4
        private int _cornerRadius = Effects.RoundedMd;
        private Color? _iconColorOverride;
        private ICommand _command;
        private object _commandParameter;

        #endregion

        #region Properties

        /// <summary>표시할 SVG 아이콘.</summary>
        [Browsable(false)]
        public SvgIcon Icon
        {
            get => _icon;
            set { _icon = value; Invalidate(); }
        }

        /// <summary>아이콘 한 변의 픽셀 크기.</summary>
        [Category("Appearance")]
        [DefaultValue(16)]
        public int IconSize
        {
            get => _iconSize;
            set { _iconSize = value; Invalidate(); }
        }

        /// <summary>모서리 반경.</summary>
        [Category("Appearance")]
        [DefaultValue(6)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; UpdateRoundedRegion(value); Invalidate(); }
        }

        /// <summary>아이콘 색 강제(미설정 시 테마 AccentPrimary).</summary>
        [Browsable(false)]
        public Color? IconColor
        {
            get => _iconColorOverride;
            set { _iconColorOverride = value; Invalidate(); }
        }

        /// <summary>
        /// 클릭 시 실행할 ICommand. OnLoad 등 런타임에 할당합니다.
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

        public VSIconButton()
        {
            Size = new Size(36, 36); // W9 × H9
            Cursor = Cursors.Hand;
            Padding = Spacing.P1;
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

        #region Factory

        /// <summary>Assets 폴더 하위 SVG 파일명으로부터 아이콘 버튼 생성.</summary>
        public static VSIconButton FromAsset(string fileName, EventHandler onClick = null)
        {
            var btn = new VSIconButton { Icon = LoadAsset(fileName) };
            if (onClick != null) btn.Click += onClick;
            return btn;
        }

        /// <summary>
        /// 앱 실행 디렉터리 기준 Assets/&lt;fileName&gt; SVG를 로드합니다.
        /// </summary>
        public static SvgIcon LoadAsset(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", fileName);
            return SvgIcon.FromFile(path);
        }

        #endregion

        #region Paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            DesignRenderer.SetHighQuality(g);

            var theme = Theme;

            // 배경 — hover/pressed 시에만 칠함 (BtnIcon 기본 Transparent)
            if (IsPressed)
                DesignRenderer.FillRoundedRect(g, ClientRectangle, theme.BgMuted, _cornerRadius);
            else if (IsHovered)
                DesignRenderer.FillRoundedRect(g, ClientRectangle, theme.BgTertiary, _cornerRadius);

            if (_icon == null) return;

            // 아이콘 중앙 배치
            float cx = Width / 2f;
            float cy = Height / 2f;
            var iconBounds = new RectangleF(cx - _iconSize / 2f, cy - _iconSize / 2f, _iconSize, _iconSize);

            var color = _iconColorOverride ?? (IsHovered ? theme.TextPrimary : theme.AccentPrimary);
            if (!Enabled) color = Color.FromArgb((int)(255 * Effects.Opacity50), color);

            _icon.Draw(g, iconBounds, color);
        }

        #endregion
    }
}
