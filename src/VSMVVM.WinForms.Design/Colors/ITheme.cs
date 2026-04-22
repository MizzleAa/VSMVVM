using System.Drawing;

namespace VSMVVM.WinForms.Design.Colors
{
    /// <summary>
    /// 테마 시맨틱 토큰 인터페이스.
    /// WPF.Design ThemeDark.xaml / ThemeLight.xaml의 Brush 키와 1:1 대응합니다.
    /// </summary>
    public interface ITheme
    {
        #region Background

        Color BgPrimary { get; }
        Color BgSecondary { get; }
        Color BgTertiary { get; }
        Color BgMuted { get; }
        Color BgInverse { get; }

        #endregion

        #region Foreground / Text

        Color TextPrimary { get; }
        Color TextSecondary { get; }
        Color TextMuted { get; }
        Color TextInverse { get; }
        Color TextPlaceholder { get; }

        #endregion

        #region Border

        Color BorderDefault { get; }
        Color BorderHover { get; }
        Color BorderFocus { get; }
        Color BorderMuted { get; }

        #endregion

        #region Accent / Primary

        Color AccentPrimary { get; }
        Color AccentPrimaryHover { get; }
        Color AccentPrimaryActive { get; }
        Color AccentPrimaryMuted { get; }
        Color Accent { get; }

        #endregion

        #region Secondary

        Color AccentSecondary { get; }
        Color AccentSecondaryHover { get; }

        #endregion

        #region Status

        Color Success { get; }
        Color SuccessHover { get; }
        Color SuccessMuted { get; }
        Color SuccessText { get; }

        Color Warning { get; }
        Color WarningHover { get; }
        Color WarningMuted { get; }
        Color WarningText { get; }

        Color Error { get; }
        Color ErrorHover { get; }
        Color ErrorMuted { get; }
        Color ErrorText { get; }
        Color Danger { get; }

        Color Info { get; }
        Color InfoHover { get; }
        Color InfoMuted { get; }
        Color InfoText { get; }

        #endregion

        #region Overlay

        Color Overlay { get; }
        Color OverlayLight { get; }

        #endregion

        #region Ring / Focus

        Color RingDefault { get; }
        Color RingOffset { get; }

        #endregion

        #region Scrollbar

        Color ScrollbarTrack { get; }
        Color ScrollbarThumb { get; }
        Color ScrollbarThumbHover { get; }

        #endregion
    }
}
