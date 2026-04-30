using System;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public readonly struct LinearScale
    {
        public double DataMin { get; }
        public double DataMax { get; }
        public double ViewStart { get; }
        public double ViewEnd { get; }
        public bool Inverted { get; }

        public LinearScale(double dataMin, double dataMax, double viewStart, double viewEnd, bool inverted = false)
        {
            DataMin = dataMin;
            DataMax = dataMax;
            ViewStart = viewStart;
            ViewEnd = viewEnd;
            Inverted = inverted;
        }

        public double DataRange => DataMax - DataMin;
        public double ViewLength => ViewEnd - ViewStart;

        public double DataToView(double d)
        {
            var range = DataMax - DataMin;
            if (range == 0 || double.IsNaN(range) || double.IsInfinity(range)) return ViewStart;
            var t = (d - DataMin) / range;
            return Inverted ? ViewEnd - t * (ViewEnd - ViewStart) : ViewStart + t * (ViewEnd - ViewStart);
        }

        public double ViewToData(double v)
        {
            var len = ViewEnd - ViewStart;
            if (len == 0) return DataMin;
            var t = Inverted ? (ViewEnd - v) / len : (v - ViewStart) / len;
            return DataMin + t * (DataMax - DataMin);
        }
    }
}
