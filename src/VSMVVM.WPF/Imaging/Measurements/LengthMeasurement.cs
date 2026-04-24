using System;
using System.Windows;

#nullable enable
namespace VSMVVM.WPF.Imaging.Measurements
{
    /// <summary>두 점 사이 거리(px). 좌표는 마스크/캔버스 픽셀 좌표계.</summary>
    public class LengthMeasurement : MeasurementBase
    {
        private Point _start;
        private Point _end;

        public Point Start
        {
            get => _start;
            set { _start = value; OnPropertyChanged(); OnPropertyChanged(nameof(Value)); OnPropertyChanged(nameof(BoundingBox)); }
        }

        public Point End
        {
            get => _end;
            set { _end = value; OnPropertyChanged(); OnPropertyChanged(nameof(Value)); OnPropertyChanged(nameof(BoundingBox)); }
        }

        public override double Value
        {
            get
            {
                double dx = _end.X - _start.X;
                double dy = _end.Y - _start.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public override string Unit => "px";

        public override Rect BoundingBox => new Rect(_start, _end);

        public override System.Collections.Generic.IReadOnlyList<Point> GetEndpoints()
            => new[] { _start, _end };

        public override void SetEndpoint(int index, Point point)
        {
            if (index == 0) Start = point;
            else if (index == 1) End = point;
        }

        public override void Translate(double dx, double dy)
        {
            Start = new Point(_start.X + dx, _start.Y + dy);
            End = new Point(_end.X + dx, _end.Y + dy);
        }
    }
}
