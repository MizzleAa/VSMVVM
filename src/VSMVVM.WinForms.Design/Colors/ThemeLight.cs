using System.Drawing;

namespace VSMVVM.WinForms.Design.Colors
{
    /// <summary>
    /// Light Theme Semantic Colors — Zinc Base + Blue Accent.
    /// WPF.Design ThemeLight.xaml과 동일한 시맨틱 매핑을 제공합니다.
    /// </summary>
    public sealed class ThemeLight : ITheme
    {
        // Background
        public Color BgPrimary => Palette.White;
        public Color BgSecondary => Palette.Zinc50;
        public Color BgTertiary => Palette.Zinc100;
        public Color BgMuted => Palette.Zinc200;
        public Color BgInverse => Palette.Zinc900;

        // Foreground / Text
        public Color TextPrimary => Palette.Zinc900;
        public Color TextSecondary => Palette.Zinc600;
        public Color TextMuted => Palette.Zinc400;
        public Color TextInverse => Palette.White;
        public Color TextPlaceholder => Palette.Zinc400;

        // Border
        public Color BorderDefault => Palette.Zinc200;
        public Color BorderHover => Palette.Zinc300;
        public Color BorderFocus => Palette.Blue500;
        public Color BorderMuted => Palette.Zinc100;

        // Accent / Primary (Blue)
        public Color AccentPrimary => Palette.Blue500;
        public Color AccentPrimaryHover => Palette.Blue600;
        public Color AccentPrimaryActive => Palette.Blue700;
        public Color AccentPrimaryMuted => Palette.Blue50;
        public Color Accent => Palette.Blue500;

        // Secondary
        public Color AccentSecondary => Palette.Zinc500;
        public Color AccentSecondaryHover => Palette.Zinc600;

        // Status: Success
        public Color Success => Palette.Green500;
        public Color SuccessHover => Palette.Green600;
        public Color SuccessMuted => Palette.Green50;
        public Color SuccessText => Palette.Green600;

        // Status: Warning
        public Color Warning => Palette.Amber500;
        public Color WarningHover => Palette.Amber600;
        public Color WarningMuted => Palette.Amber50;
        public Color WarningText => Palette.Amber600;

        // Status: Error / Danger
        public Color Error => Palette.Red500;
        public Color ErrorHover => Palette.Red600;
        public Color ErrorMuted => Palette.Red50;
        public Color ErrorText => Palette.Red600;
        public Color Danger => Palette.Red500;

        // Status: Info
        public Color Info => Palette.Sky500;
        public Color InfoHover => Palette.Sky600;
        public Color InfoMuted => Palette.Sky50;
        public Color InfoText => Palette.Sky600;

        // Overlay
        public Color Overlay => Color.FromArgb(178, 0, 0, 0);
        public Color OverlayLight => Color.FromArgb(128, 0, 0, 0);

        // Ring / Focus
        public Color RingDefault => Palette.Blue500;
        public Color RingOffset => Palette.White;

        // Scrollbar
        public Color ScrollbarTrack => Palette.Zinc100;
        public Color ScrollbarThumb => Palette.Zinc300;
        public Color ScrollbarThumbHover => Palette.Zinc400;
    }
}
