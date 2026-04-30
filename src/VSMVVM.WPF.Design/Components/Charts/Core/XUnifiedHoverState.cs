using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public sealed class XUnifiedHoverPoint
    {
        public int SeriesIndex { get; }
        public string SeriesTitle { get; }
        public Brush Brush { get; }
        public double DataX { get; }
        public double DataY { get; }
        public Point ScreenPoint { get; }

        public XUnifiedHoverPoint(int seriesIndex, string seriesTitle, Brush brush,
                                   double dataX, double dataY, Point screenPoint)
        {
            SeriesIndex = seriesIndex;
            SeriesTitle = seriesTitle;
            Brush = brush;
            DataX = dataX;
            DataY = dataY;
            ScreenPoint = screenPoint;
        }
    }

    public sealed class XUnifiedHoverState
    {
        public bool HasValue { get; }
        public double DataX { get; }
        public Point ScreenPoint { get; }
        public IReadOnlyList<XUnifiedHoverPoint> Points { get; }

        public static XUnifiedHoverState Empty { get; } = new XUnifiedHoverState();

        private XUnifiedHoverState() { Points = System.Array.Empty<XUnifiedHoverPoint>(); }

        public XUnifiedHoverState(double dataX, Point screenPoint, IReadOnlyList<XUnifiedHoverPoint> points)
        {
            HasValue = true;
            DataX = dataX;
            ScreenPoint = screenPoint;
            Points = points ?? System.Array.Empty<XUnifiedHoverPoint>();
        }
    }
}
