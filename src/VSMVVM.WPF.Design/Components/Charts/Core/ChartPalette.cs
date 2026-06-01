using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public static class ChartPalette
    {
        private static readonly string[] PaletteKeys = new[]
        {
            "Blue500Brush", "Red500Brush", "Green500Brush", "Amber500Brush",
            "Violet500Brush", "Pink500Brush", "Cyan500Brush", "Orange500Brush",
            "Indigo500Brush", "Emerald500Brush", "Sky500Brush", "Rose500Brush",
            "Fuchsia500Brush", "Teal500Brush"
        };

        public static IList<Brush> GetPalette(FrameworkElement context)
        {
            var list = new List<Brush>(PaletteKeys.Length);
            foreach (var key in PaletteKeys)
            {
                var b = context.TryFindResource(key) as Brush;
                if (b != null) list.Add(b);
            }
            if (list.Count == 0) list.AddRange(GetFallbackPalette());
            return list;
        }

        /// <summary>
        /// FrameworkElement 컨텍스트가 없는 곳(ViewModel 등)에서 사용. <see cref="Application.Current"/>.Resources 에서 직접 lookup.
        /// 앱이 초기화되기 전이거나 토큰이 없으면 fallback 4색.
        /// </summary>
        public static IList<Brush> GetPalette()
        {
            var list = new List<Brush>(PaletteKeys.Length);
            var app = Application.Current;
            if (app?.Resources != null)
            {
                foreach (var key in PaletteKeys)
                {
                    if (app.Resources[key] is Brush b) list.Add(b);
                }
            }
            if (list.Count == 0) list.AddRange(GetFallbackPalette());
            return list;
        }

        private static IEnumerable<Brush> GetFallbackPalette()
        {
            yield return new SolidColorBrush(Color.FromRgb(59, 130, 246));
            yield return new SolidColorBrush(Color.FromRgb(239, 68, 68));
            yield return new SolidColorBrush(Color.FromRgb(34, 197, 94));
            yield return new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
    }
}
