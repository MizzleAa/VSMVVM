using System.Drawing;

namespace VSMVVM.WinForms.Design.Colors
{
    /// <summary>
    /// Dark Theme Semantic Colors — Zinc Base + Blue Accent.
    /// WPF.Design ThemeDark.xaml과 동일한 시맨틱 매핑을 제공합니다.
    /// </summary>
    public sealed class ThemeDark : ITheme
    {
        // Background (Zinc)
        public Color BgPrimary => Palette.Zinc950;
        public Color BgSecondary => Palette.Zinc900;
        public Color BgTertiary => Palette.Zinc800;
        public Color BgMuted => Palette.Zinc700;
        public Color BgInverse => Palette.Zinc50;

        // Foreground / Text (Zinc)
        public Color TextPrimary => Palette.Zinc50;
        public Color TextSecondary => Palette.Zinc300;
        public Color TextMuted => Palette.Zinc500;
        public Color TextInverse => Palette.Zinc900;
        public Color TextPlaceholder => Palette.Zinc500;

        // Border (Zinc)
        public Color BorderDefault => Palette.Zinc700;
        public Color BorderHover => Palette.Zinc600;
        public Color BorderFocus => Palette.Blue400;
        public Color BorderMuted => Palette.Zinc800;

        // Accent / Primary (Blue)
        public Color AccentPrimary => Palette.Blue500;
        public Color AccentPrimaryHover => Palette.Blue400;
        public Color AccentPrimaryActive => Palette.Blue300;
        public Color AccentPrimaryMuted => Palette.Blue900;
        public Color Accent => Palette.Blue500;

        // Secondary (Zinc)
        public Color AccentSecondary => Palette.Zinc400;
        public Color AccentSecondaryHover => Palette.Zinc300;

        // Status: Success
        public Color Success => Palette.Green500;
        public Color SuccessHover => Palette.Green400;
        public Color SuccessMuted => Palette.Green950;
        public Color SuccessText => Palette.Green400;

        // Status: Warning
        public Color Warning => Palette.Amber500;
        public Color WarningHover => Palette.Amber400;
        public Color WarningMuted => Palette.Amber950;
        public Color WarningText => Palette.Amber400;

        // Status: Error / Danger
        public Color Error => Palette.Red500;
        public Color ErrorHover => Palette.Red400;
        public Color ErrorMuted => Palette.Red950;
        public Color ErrorText => Palette.Red400;
        public Color Danger => Palette.Red500;

        // Status: Info
        public Color Info => Palette.Sky500;
        public Color InfoHover => Palette.Sky400;
        public Color InfoMuted => Palette.Sky950;
        public Color InfoText => Palette.Sky400;

        // Overlay
        public Color Overlay => Color.FromArgb(178, 0, 0, 0);        // Black 70%
        public Color OverlayLight => Color.FromArgb(128, 0, 0, 0);   // Black 50%

        // Ring / Focus (Blue)
        public Color RingDefault => Palette.Blue400;
        public Color RingOffset => Palette.Zinc900;

        // Scrollbar (Zinc)
        public Color ScrollbarTrack => Palette.Zinc800;
        public Color ScrollbarThumb => Palette.Zinc600;
        public Color ScrollbarThumbHover => Palette.Zinc500;
    }
}
