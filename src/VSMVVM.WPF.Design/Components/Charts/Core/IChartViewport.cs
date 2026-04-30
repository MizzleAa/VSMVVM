using System;
using System.Windows;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public interface IChartViewport
    {
        double ZoomLevel { get; }
        double ContentWidth { get; }
        double ContentHeight { get; }
        double ViewportWidth { get; }
        double ViewportHeight { get; }
        Point ScreenToCanvas(Point screenPoint);
        void SetOffset(double x, double y);
        void SetZoom(double zoom);
        void FitToContent();
        void ZoomToBounds(Rect bounds, double padding = 0.95);
        event EventHandler ViewportChanged;
        event SizeChangedEventHandler ViewportSizeChanged;
    }
}
