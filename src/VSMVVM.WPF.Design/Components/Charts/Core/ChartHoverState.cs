using System.Windows;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public readonly struct ChartHoverState
    {
        public bool HasValue { get; }
        public int SeriesIndex { get; }
        public int PointIndex { get; }
        public double DataX { get; }
        public double DataY { get; }
        public Point ScreenPoint { get; }
        public string SeriesTitle { get; }
        public object Tag { get; }

        public ChartHoverState(int seriesIndex, int pointIndex, double dataX, double dataY, Point screenPoint, string seriesTitle)
            : this(seriesIndex, pointIndex, dataX, dataY, screenPoint, seriesTitle, null)
        { }

        public ChartHoverState(int seriesIndex, int pointIndex, double dataX, double dataY, Point screenPoint, string seriesTitle, object tag)
        {
            HasValue = true;
            SeriesIndex = seriesIndex;
            PointIndex = pointIndex;
            DataX = dataX;
            DataY = dataY;
            ScreenPoint = screenPoint;
            SeriesTitle = seriesTitle;
            Tag = tag;
        }

        public static ChartHoverState Empty => default;
    }
}
