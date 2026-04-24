using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VSMVVM.WPF.Imaging.Measurements;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 측정(길이·각도) 주석을 그리는 레이어. MaskLayer 와 나란히 LayeredCanvas 자식으로 배치.
    /// Width/Height 는 이미지 픽셀 크기로 바인딩하면 LayeredCanvas transform 에 따라 자동 scale.
    /// </summary>
    public class MeasurementLayer : FrameworkElement
    {
        public static readonly DependencyProperty MeasurementsProperty =
            DependencyProperty.Register(nameof(Measurements), typeof(MeasurementCollection), typeof(MeasurementLayer),
                new PropertyMetadata(null, OnMeasurementsChanged));

        public static readonly DependencyProperty StrokeBrushProperty =
            DependencyProperty.Register(nameof(StrokeBrush), typeof(Brush), typeof(MeasurementLayer),
                new PropertyMetadata(Brushes.Cyan, (d, _) => ((MeasurementLayer)d).InvalidateVisual()));

        public static readonly DependencyProperty LabelBrushProperty =
            DependencyProperty.Register(nameof(LabelBrush), typeof(Brush), typeof(MeasurementLayer),
                new PropertyMetadata(Brushes.White, (d, _) => ((MeasurementLayer)d).InvalidateVisual()));

        public static readonly DependencyProperty SelectedStrokeBrushProperty =
            DependencyProperty.Register(nameof(SelectedStrokeBrush), typeof(Brush), typeof(MeasurementLayer),
                new PropertyMetadata(Brushes.Yellow, (d, _) => ((MeasurementLayer)d).InvalidateVisual()));

        public static readonly DependencyProperty SelectedMeasurementProperty =
            DependencyProperty.Register(nameof(SelectedMeasurement), typeof(MeasurementBase), typeof(MeasurementLayer),
                new PropertyMetadata(null, OnSelectedMeasurementChanged));

        private static void OnSelectedMeasurementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (MeasurementLayer)d;
            if (e.OldValue is MeasurementBase oldM) oldM.IsSelected = false;
            if (e.NewValue is MeasurementBase newM) newM.IsSelected = true;
            self.InvalidateVisual();
        }

        public MeasurementBase? SelectedMeasurement
        {
            get => (MeasurementBase?)GetValue(SelectedMeasurementProperty);
            set => SetValue(SelectedMeasurementProperty, value);
        }

        public MeasurementCollection? Measurements
        {
            get => (MeasurementCollection?)GetValue(MeasurementsProperty);
            set => SetValue(MeasurementsProperty, value);
        }

        public Brush StrokeBrush { get => (Brush)GetValue(StrokeBrushProperty); set => SetValue(StrokeBrushProperty, value); }
        public Brush LabelBrush { get => (Brush)GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
        public Brush SelectedStrokeBrush { get => (Brush)GetValue(SelectedStrokeBrushProperty); set => SetValue(SelectedStrokeBrushProperty, value); }

        // 드래그 세션 상태.
        private MeasurementBase? _dragTarget;
        private int _dragPointIndex = -1; // -1 = 전체 translate, >=0 = 해당 endpoint 만 이동
        private Point _dragStart;
        private System.Collections.Generic.List<Point>? _dragStartPoints;

        private const double HitRadius = 6.0;

        /// <summary>현재 RenderTransform scale(=zoom). 핸들/펜/폰트 화면 크기 고정을 위해 1/z 보정에 사용.</summary>
        private double GetZoom()
        {
            if (RenderTransform is MatrixTransform mt && mt.Matrix.M11 > 0.0001)
                return mt.Matrix.M11;
            return 1.0;
        }

        public MeasurementLayer()
        {
            IsHitTestVisible = false; // Select 모드에서만 외부가 true 로 토글.
            SnapsToDevicePixels = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var list = Measurements;
            if (list == null) return;

            var p = e.GetPosition(this);

            // Hit 반경을 zoom 독립으로 유지 (화면 기준 6 DIU).
            double z = GetZoom();
            double hit = HitRadius / z;

            // 1) 가장 가까운 endpoint 탐색 (hit radius 내).
            MeasurementBase? pointTarget = null;
            int pointIdx = -1;
            double bestDist2 = hit * hit;
            foreach (var m in list)
            {
                if (!m.IsVisible) continue;
                var eps = m.GetEndpoints();
                for (int i = 0; i < eps.Count; i++)
                {
                    var ep = eps[i];
                    double dx = p.X - ep.X, dy = p.Y - ep.Y;
                    double d2 = dx * dx + dy * dy;
                    if (d2 <= bestDist2)
                    {
                        bestDist2 = d2;
                        pointTarget = m;
                        pointIdx = i;
                    }
                }
            }

            if (pointTarget != null)
            {
                BeginDrag(pointTarget, pointIdx, p);
                e.Handled = true;
                return;
            }

            // 2) BBox 포함(선 본체 근접) 체크.
            foreach (var m in list)
            {
                if (!m.IsVisible) continue;
                var b = m.BoundingBox;
                // 선형 측정은 bbox 크기가 작을 수 있으니 약간 inflate.
                var expanded = new Rect(b.X - hit, b.Y - hit, b.Width + hit * 2, b.Height + hit * 2);
                if (expanded.Contains(p))
                {
                    BeginDrag(m, -1, p);
                    e.Handled = true;
                    return;
                }
            }
            // hit 없음 → bubble.
        }

        private void BeginDrag(MeasurementBase target, int pointIdx, Point pos)
        {
            _dragTarget = target;
            _dragPointIndex = pointIdx;
            _dragStart = pos;
            var eps = target.GetEndpoints();
            _dragStartPoints = new System.Collections.Generic.List<Point>(eps);
            SelectedMeasurement = target;
            CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragTarget == null || _dragStartPoints == null) return;
            var p = e.GetPosition(this);
            double dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;

            if (_dragPointIndex >= 0)
            {
                // 개별 point.
                var start = _dragStartPoints[_dragPointIndex];
                _dragTarget.SetEndpoint(_dragPointIndex, new Point(start.X + dx, start.Y + dy));
            }
            else
            {
                // 전체 translate — 시작 시점 endpoints 기준으로 절대값 재설정.
                for (int i = 0; i < _dragStartPoints.Count; i++)
                {
                    var s = _dragStartPoints[i];
                    _dragTarget.SetEndpoint(i, new Point(s.X + dx, s.Y + dy));
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragTarget == null) return;
            _dragTarget = null;
            _dragPointIndex = -1;
            _dragStartPoints = null;
            ReleaseMouseCapture();
            e.Handled = true;
        }

        private static void OnMeasurementsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (MeasurementLayer)d;
            if (e.OldValue is MeasurementCollection oldC)
            {
                oldC.CollectionChanged -= self.OnMeasurementsCollectionChanged;
                foreach (var m in oldC) m.PropertyChanged -= self.OnItemPropertyChanged;
            }
            if (e.NewValue is MeasurementCollection newC)
            {
                newC.CollectionChanged += self.OnMeasurementsCollectionChanged;
                foreach (var m in newC) m.PropertyChanged += self.OnItemPropertyChanged;
            }
            self.InvalidateVisual();
        }

        private void OnMeasurementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (MeasurementBase m in e.NewItems) m.PropertyChanged += OnItemPropertyChanged;
            if (e.OldItems != null)
                foreach (MeasurementBase m in e.OldItems) m.PropertyChanged -= OnItemPropertyChanged;
            InvalidateVisual();
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var list = Measurements;
            if (list == null || list.Count == 0) return;

            // 모든 drawing 상수를 zoom 으로 보정 → 화면 기준 크기 고정.
            double z = GetZoom();
            var pen = new Pen(StrokeBrush, 1.5 / z); pen.Freeze();
            var selPen = new Pen(SelectedStrokeBrush, 2.5 / z); selPen.Freeze();
            double handleR = 3.0 / z;
            double fontSize = 12.0 / z;
            double labelDx = 6.0 / z;
            double labelDy = 2.0 / z;
            double labelPad = 2.0 / z;

            foreach (var m in list)
            {
                if (!m.IsVisible) continue;
                var p = m.IsSelected ? selPen : pen;
                switch (m)
                {
                    case LengthMeasurement lm: DrawLength(dc, p, lm, handleR, fontSize, labelDx, labelDy, labelPad); break;
                    case AngleMeasurement am: DrawAngle(dc, p, am, handleR, fontSize, labelDx, labelDy, labelPad); break;
                }
            }
        }

        private void DrawLength(DrawingContext dc, Pen pen, LengthMeasurement m,
            double handleR, double fontSize, double labelDx, double labelDy, double labelPad)
        {
            dc.DrawLine(pen, m.Start, m.End);
            DrawHandle(dc, pen, m.Start, handleR);
            DrawHandle(dc, pen, m.End, handleR);
            var mid = new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
            DrawLabel(dc, $"{m.Value:F1} {m.Unit}", mid, fontSize, labelDx, labelDy, labelPad);
        }

        private void DrawAngle(DrawingContext dc, Pen pen, AngleMeasurement m,
            double handleR, double fontSize, double labelDx, double labelDy, double labelPad)
        {
            dc.DrawLine(pen, m.Vertex, m.P1);
            dc.DrawLine(pen, m.Vertex, m.P2);
            DrawHandle(dc, pen, m.P1, handleR);
            DrawHandle(dc, pen, m.Vertex, handleR);
            DrawHandle(dc, pen, m.P2, handleR);
            DrawLabel(dc, $"{m.Value:F1} {m.Unit}", m.Vertex, fontSize, labelDx, labelDy, labelPad);
        }

        private static void DrawHandle(DrawingContext dc, Pen pen, Point p, double r)
        {
            dc.DrawEllipse(Brushes.Black, pen, p, r, r);
        }

        private void DrawLabel(DrawingContext dc, string text, Point anchor,
            double fontSize, double dx, double dy, double pad)
        {
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), fontSize, LabelBrush, 96);
            var pt = new Point(anchor.X + dx, anchor.Y - ft.Height - dy);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), null,
                new Rect(pt.X - pad, pt.Y, ft.Width + pad * 2, ft.Height));
            dc.DrawText(ft, pt);
        }
    }
}
