using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public sealed class FormattedTextCache
    {
        private const int MaxEntries = 512;
        private readonly Dictionary<(string text, double size, Brush brush, string typefaceKey), FormattedText> _cache = new();
        private readonly Typeface _defaultTypeface = new Typeface("Segoe UI");
        private double _pixelsPerDip = 1.0;

        public void SetPixelsPerDip(double dpi) => _pixelsPerDip = dpi <= 0 ? 1.0 : dpi;

        public FormattedText Get(string text, double size, Brush brush)
        {
            return Get(text, size, brush, null, FontWeights.Normal);
        }

        public FormattedText Get(string text, double size, Brush brush, FontFamily family, FontWeight weight)
        {
            if (text == null) text = string.Empty;
            var typeface = (family != null)
                ? new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal)
                : _defaultTypeface;
            var key = (text, size, brush, family != null ? family.Source + "|" + weight.ToOpenTypeWeight() : "default");
            if (_cache.TryGetValue(key, out var ft)) return ft;
            if (_cache.Count >= MaxEntries) _cache.Clear();
            ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                typeface, size, brush, _pixelsPerDip);
            _cache[key] = ft;
            return ft;
        }

        public void Clear() => _cache.Clear();
    }
}
