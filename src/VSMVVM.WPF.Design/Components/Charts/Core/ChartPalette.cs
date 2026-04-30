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
            if (list.Count == 0)
            {
                list.Add(new SolidColorBrush(Color.FromRgb(59, 130, 246)));
                list.Add(new SolidColorBrush(Color.FromRgb(239, 68, 68)));
                list.Add(new SolidColorBrush(Color.FromRgb(34, 197, 94)));
                list.Add(new SolidColorBrush(Color.FromRgb(245, 158, 11)));
            }
            return list;
        }
    }
}
