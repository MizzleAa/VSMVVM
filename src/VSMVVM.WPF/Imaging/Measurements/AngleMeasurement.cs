using System;
using System.Windows;

#nullable enable
namespace VSMVVM.WPF.Imaging.Measurements
{
    /// <summary>세 점 각도(°). Vertex 가 꼭지점. P1-Vertex-P2 사이 각.</summary>
    public class AngleMeasurement : MeasurementBase
    {
        private Point _p1;
        private Point _vertex;
        private Point _p2;

        public Point P1
        {
            get => _p1;
            set { _p1 = value; Raise(); }
        }

        public Point Vertex
        {
            get => _vertex;
            set { _vertex = value; Raise(); }
        }

        public Point P2
        {
            get => _p2;
            set { _p2 = value; Raise(); }
        }

        private void Raise()
        {
            OnPropertyChanged(nameof(P1));
            OnPropertyChanged(nameof(Vertex));
            OnPropertyChanged(nameof(P2));
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(BoundingBox));
        }

        public override double Value
        {
            get
            {
                double v1x = _p1.X - _vertex.X, v1y = _p1.Y - _vertex.Y;
                double v2x = _p2.X - _vertex.X, v2y = _p2.Y - _vertex.Y;
                double n1 = Math.Sqrt(v1x * v1x + v1y * v1y);
                double n2 = Math.Sqrt(v2x * v2x + v2y * v2y);
                if (n1 == 0 || n2 == 0) return 0;
                double cos = (v1x * v2x + v1y * v2y) / (n1 * n2);
                cos = Math.Max(-1.0, Math.Min(1.0, cos));
                return Math.Acos(cos) * 180.0 / Math.PI;
            }
        }

        public override string Unit => "°";

        public override Rect BoundingBox
        {
            get
            {
                double minX = Math.Min(_p1.X, Math.Min(_vertex.X, _p2.X));
                double minY = Math.Min(_p1.Y, Math.Min(_vertex.Y, _p2.Y));
                double maxX = Math.Max(_p1.X, Math.Max(_vertex.X, _p2.X));
                double maxY = Math.Max(_p1.Y, Math.Max(_vertex.Y, _p2.Y));
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
        }

        public override System.Collections.Generic.IReadOnlyList<Point> GetEndpoints()
            => new[] { _p1, _vertex, _p2 };

        public override void SetEndpoint(int index, Point point)
        {
            switch (index)
            {
                case 0: P1 = point; break;
                case 1: Vertex = point; break;
                case 2: P2 = point; break;
            }
        }

        public override void Translate(double dx, double dy)
        {
            P1 = new Point(_p1.X + dx, _p1.Y + dy);
            Vertex = new Point(_vertex.X + dx, _vertex.Y + dy);
            P2 = new Point(_p2.X + dx, _p2.Y + dy);
        }
    }
}
