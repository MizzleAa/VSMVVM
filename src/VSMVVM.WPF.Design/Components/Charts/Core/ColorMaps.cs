using System;
using System.Windows.Media;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    /// <summary>
    /// Matplotlib 호환 perceptually-uniform colormap 5종.
    /// 9개 control point만 저장하고 sample 시 선형 보간 — 시각적으로 256단계 LUT와 구분 불가.
    /// 각 control point는 Matplotlib 원본 LUT의 t = 0, 0.125, 0.25, ..., 1.0 위치 RGB(byte).
    /// </summary>
    public static class ColorMaps
    {
        // Viridis (Matplotlib default)
        private static readonly byte[][] Viridis = {
            new byte[]{ 68,  1, 84}, new byte[]{ 72, 36,117}, new byte[]{ 64, 67,135},
            new byte[]{ 52, 94,141}, new byte[]{ 41,120,142}, new byte[]{ 32,144,140},
            new byte[]{ 34,167,132}, new byte[]{ 68,190,112}, new byte[]{121,209, 81},
            new byte[]{189,222, 38}, new byte[]{253,231, 36},
        };

        private static readonly byte[][] Plasma = {
            new byte[]{ 12,  7,134}, new byte[]{ 64,  3,156}, new byte[]{106,  0,167},
            new byte[]{143, 13,164}, new byte[]{176, 42,143}, new byte[]{204, 71,120},
            new byte[]{225,100, 98}, new byte[]{242,132, 75}, new byte[]{252,166, 53},
            new byte[]{252,203, 39}, new byte[]{239,248, 33},
        };

        private static readonly byte[][] Inferno = {
            new byte[]{  0,  0,  3}, new byte[]{ 31, 11, 70}, new byte[]{ 75, 12,107},
            new byte[]{120, 28,109}, new byte[]{165, 44, 96}, new byte[]{207, 68, 70},
            new byte[]{237,104, 37}, new byte[]{252,148,  9}, new byte[]{251,193, 32},
            new byte[]{246,238,113}, new byte[]{252,255,164},
        };

        private static readonly byte[][] Magma = {
            new byte[]{  0,  0,  3}, new byte[]{ 27, 12, 65}, new byte[]{ 64, 15,110},
            new byte[]{106, 23,131}, new byte[]{147, 38,141}, new byte[]{190, 55,134},
            new byte[]{229, 80,108}, new byte[]{251,121, 91}, new byte[]{254,170,114},
            new byte[]{254,217,158}, new byte[]{251,253,191},
        };

        private static readonly byte[][] Cividis = {
            new byte[]{  0, 32, 76}, new byte[]{  0, 50,113}, new byte[]{ 49, 70,117},
            new byte[]{ 86, 89,118}, new byte[]{114,108,121}, new byte[]{142,128,121},
            new byte[]{172,150,116}, new byte[]{205,173,107}, new byte[]{237,196, 85},
            new byte[]{252,222, 51}, new byte[]{255,234, 70},
        };

        public static Color Sample(ColorMap map, double t)
        {
            if (!double.IsFinite(t)) t = 0;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            var lut = LutFor(map);
            var n = lut.Length - 1;
            var pos = t * n;
            var i = (int)pos;
            if (i >= n) return Color.FromRgb(lut[n][0], lut[n][1], lut[n][2]);
            var f = pos - i;
            var a = lut[i];
            var b = lut[i + 1];
            var r = (byte)(a[0] + (b[0] - a[0]) * f);
            var g = (byte)(a[1] + (b[1] - a[1]) * f);
            var bl = (byte)(a[2] + (b[2] - a[2]) * f);
            return Color.FromRgb(r, g, bl);
        }

        public static Color SampleCustom(Color low, Color high, double t)
        {
            if (!double.IsFinite(t)) t = 0;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            byte L(byte x, byte y) => (byte)(x + (y - x) * t);
            return Color.FromArgb(L(low.A, high.A), L(low.R, high.R), L(low.G, high.G), L(low.B, high.B));
        }

        private static byte[][] LutFor(ColorMap map) => map switch
        {
            ColorMap.Plasma => Plasma,
            ColorMap.Inferno => Inferno,
            ColorMap.Magma => Magma,
            ColorMap.Cividis => Cividis,
            _ => Viridis,
        };

        /// <summary>colormap 11개 stop을 가진 frozen LinearGradientBrush (color bar용).</summary>
        public static LinearGradientBrush CreateGradientBrush(ColorMap map, bool vertical = true)
        {
            var lut = LutFor(map);
            var stops = new GradientStopCollection();
            for (var i = 0; i < lut.Length; i++)
            {
                var t = (double)i / (lut.Length - 1);
                var c = Color.FromRgb(lut[i][0], lut[i][1], lut[i][2]);
                // 컬러바는 위가 high, 아래가 low가 자연스러우므로 t 뒤집기
                var stop = vertical ? 1.0 - t : t;
                stops.Add(new GradientStop(c, stop));
            }
            var brush = vertical
                ? new LinearGradientBrush(stops, new System.Windows.Point(0, 0), new System.Windows.Point(0, 1))
                : new LinearGradientBrush(stops, new System.Windows.Point(0, 0), new System.Windows.Point(1, 0));
            brush.Freeze();
            return brush;
        }

        public static LinearGradientBrush CreateCustomGradient(Color low, Color high, bool vertical = true)
        {
            var stops = new GradientStopCollection
            {
                new GradientStop(vertical ? high : low, 0),
                new GradientStop(vertical ? low : high, 1),
            };
            var brush = vertical
                ? new LinearGradientBrush(stops, new System.Windows.Point(0, 0), new System.Windows.Point(0, 1))
                : new LinearGradientBrush(stops, new System.Windows.Point(0, 0), new System.Windows.Point(1, 0));
            brush.Freeze();
            return brush;
        }
    }
}
