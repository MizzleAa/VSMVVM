using System;
using System.IO;
using System.Reflection;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace VSMVVM.WPF.Scheduler.Editor.Themes
{
    /// <summary>
    /// 두 개의 embedded XSHD 정의(Dark/Light)를 로드하고 캐시하여 AvalonEdit.IHighlightingDefinition 으로 노출한다.
    /// 호스트 앱의 테마가 바뀌면 CodeNodeEditor가 Load(theme)를 다시 호출하여 즉시 추종.
    /// </summary>
    public static class HighlightingThemeService
    {
        private const string DarkResource = "VSMVVM.WPF.Scheduler.Editor.Themes.CSharp-Dark.xshd";
        private const string LightResource = "VSMVVM.WPF.Scheduler.Editor.Themes.CSharp-Light.xshd";

        private static IHighlightingDefinition _darkCache;
        private static IHighlightingDefinition _lightCache;

        /// <summary>지정 테마에 해당하는 신택스 정의를 반환 (캐시됨).</summary>
        public static IHighlightingDefinition Load(EditorTheme theme)
        {
            switch (theme)
            {
                case EditorTheme.Light:
                    return _lightCache ??= LoadFromResource(LightResource);
                case EditorTheme.Dark:
                default:
                    return _darkCache ??= LoadFromResource(DarkResource);
            }
        }

        private static IHighlightingDefinition LoadFromResource(string resourceName)
        {
            var asm = typeof(HighlightingThemeService).GetTypeInfo().Assembly;
            using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{resourceName}' not found. " +
                    "Ensure the Themes/*.xshd files are included as <EmbeddedResource>.");
            using var reader = XmlReader.Create(stream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
    }
}
