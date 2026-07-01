using FluentAssertions;
using VSMVVM.WPF.Scheduler.Editor.Themes;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Editor.Tests
{
    public class HighlightingThemeServiceTests
    {
        [Fact]
        public void Load_Dark_ReturnsCSharpDefinition()
        {
            var def = HighlightingThemeService.Load(EditorTheme.Dark);

            def.Should().NotBeNull();
            def.Name.Should().Be("C#");
        }

        [Fact]
        public void Load_Light_ReturnsCSharpDefinition()
        {
            var def = HighlightingThemeService.Load(EditorTheme.Light);

            def.Should().NotBeNull();
            def.Name.Should().Be("C#");
        }

        [Fact]
        public void Load_IsCached_ReturnsSameInstance_AcrossCalls()
        {
            var first = HighlightingThemeService.Load(EditorTheme.Dark);
            var second = HighlightingThemeService.Load(EditorTheme.Dark);

            second.Should().BeSameAs(first);
        }

        [Fact]
        public void Dark_AndLight_AreDifferentInstances()
        {
            var dark = HighlightingThemeService.Load(EditorTheme.Dark);
            var light = HighlightingThemeService.Load(EditorTheme.Light);

            dark.Should().NotBeSameAs(light);
        }

        [Fact]
        public void Dark_HasKeywordColor_BlueAccent()
        {
            var dark = HighlightingThemeService.Load(EditorTheme.Dark);
            var keywordColor = dark.GetNamedColor("Keyword");

            keywordColor.Should().NotBeNull();
            keywordColor.Foreground.Should().NotBeNull();
        }
    }
}
