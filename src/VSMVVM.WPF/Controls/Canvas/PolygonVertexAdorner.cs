using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using VSMVVM.WPF.Imaging;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// <see cref="MaskInstance.PolygonPoints"/> 가 있는 인스턴스의 꼭짓점 편집 Adorner.
    /// 드래그 = 점 이동, edge 중앙 핸들 클릭/드래그 = 점 삽입, 우클릭/Del = 점 삭제 (≥3 점 유지).
    /// MouseUp 마다 <see cref="CommitRequested"/> 발화 → MaskBehavior 가 MaskLayer.RepaintPolygon + Undo push.
    /// </summary>
    public sealed class PolygonVertexAdorner : Adorner
    {
        private readonly MaskLayer _mask;
        private MaskInstance _instance;
        private readonly List<Point> _points; // 픽셀 좌표. 편집 세션 내 현재 상태.
        private int _dragVertexIndex = -1;
        private const double HandleSize = 8.0;
        private const double EdgeHandleSize = 6.0;
        private const double EdgeHitTolerance = 4.0;

        public PolygonVertexAdorner(MaskLayer mask, MaskInstance instance) : base(mask)
        {
            _mask = mask;
            _instance = instance;
            _points = instance.PolygonPoints != null
                ? new List<Point>(instance.PolygonPoints)
                : new List<Point>();
            IsHitTestVisible = true;
            Focusable = true;
        }

        public MaskInstance Instance => _instance;

        /// <summary>마우스 Up 시 발화. 편집된 점 리스트 전달.</summary>
        public event EventHandler<PolygonVertexCommitEventArgs>? CommitRequested;

        private double GetZoom()
        {
            if (_mask.RenderTransform is MatrixTransform mt && mt.Matrix.M11 > 0.0001)
                return mt.Matrix.M11;
            return 1.0;
        }

        private Point PixelToLocal(Point p)
        {
            var displayW = _mask.ActualWidth > 0 ? _mask.ActualWidth : _mask.MaskWidth;
            var displayH = _mask.ActualHeight > 0 ? _mask.ActualHeight : _mask.MaskHeight;
            if (_mask.MaskWidth == 0 || _mask.MaskHeight == 0) return p;
            return new Point(p.X * displayW / _mask.MaskWidth, p.Y * displayH / _mask.MaskHeight);
        }

        private Point LocalToPixel(Point local)
        {
            var displayW = _mask.ActualWidth > 0 ? _mask.ActualWidth : _mask.MaskWidth;
            var displayH = _mask.ActualHeight > 0 ? _mask.ActualHeight : _mask.MaskHeight;
            if (displayW == 0 || displayH == 0) return local;
            return new Point(local.X * _mask.MaskWidth / displayW, local.Y * _mask.MaskHeight / displayH);
        }

        private Point ClampPixel(Point p)
        {
            return new Point(
                Math.Max(0, Math.Min(_mask.MaskWidth, p.X)),
                Math.Max(0, Math.Min(_mask.MaskHeight, p.Y)));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (_points.Count < 3) return;

            // AdornerLayer clip.
            var clipBounds = new Rect(0, 0,
                _mask.ActualWidth > 0 ? _mask.ActualWidth : _mask.MaskWidth,
                _mask.ActualHeight > 0 ? _mask.ActualHeight : _mask.MaskHeight);
            dc.PushClip(new RectangleGeometry(clipBounds));
            try
            {
                double z = GetZoom();
                double h = HandleSize / z;
                double eh = EdgeHandleSize / z;

                // 노란 실선 외곽선.
                var pen = new Pen(Brushes.Yellow, 1.5 / z);
                pen.Freeze();
                var figure = new PathFigure { StartPoint = PixelToLocal(_points[0]), IsClosed = true };
                for (int i = 1; i < _points.Count; i++)
                    figure.Segments.Add(new LineSegment(PixelToLocal(_points[i]), true));
                var geo = new PathGeometry();
                geo.Figures.Add(figure);
                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);

                // Edge 중앙 핸들 (새 점 삽입용) — 작고 반투명.
                var edgeFill = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)); edgeFill.Freeze();
                var edgeBorder = new Pen(Brushes.DarkGray, 1.0 / z); edgeBorder.Freeze();
                for (int i = 0; i < _points.Count; i++)
                {
                    int j = (i + 1) % _points.Count;
                    var mid = new Point((_points[i].X + _points[j].X) / 2, (_points[i].Y + _points[j].Y) / 2);
                    var l = PixelToLocal(mid);
                    dc.DrawRectangle(edgeFill, edgeBorder, new Rect(l.X - eh / 2, l.Y - eh / 2, eh, eh));
                }

                // Vertex 핸들 — 흰색 사각.
                var vertBorder = new Pen(Brushes.Black, 1.0 / z); vertBorder.Freeze();
                foreach (var p in _points)
                {
                    var l = PixelToLocal(p);
                    dc.DrawRectangle(Brushes.White, vertBorder, new Rect(l.X - h / 2, l.Y - h / 2, h, h));
                }
            }
            finally
            {
                dc.Pop();
            }
        }

        /// <summary>Local 점 p 가 특정 vertex 핸들 위인지. true 면 index 반환, 아니면 -1.</summary>
        private int HitTestVertex(Point p)
        {
            double z = GetZoom();
            double r = HandleSize / z;
            for (int i = 0; i < _points.Count; i++)
            {
                var l = PixelToLocal(_points[i]);
                if (Math.Abs(p.X - l.X) <= r && Math.Abs(p.Y - l.Y) <= r) return i;
            }
            return -1;
        }

        /// <summary>Edge 중앙 핸들 hit. 삽입 후 인덱스 반환, 아니면 -1 (_points 수정 전).</summary>
        private int HitTestEdgeMidpoint(Point p)
        {
            double z = GetZoom();
            double r = EdgeHandleSize / z;
            for (int i = 0; i < _points.Count; i++)
            {
                int j = (i + 1) % _points.Count;
                var mid = new Point((_points[i].X + _points[j].X) / 2, (_points[i].Y + _points[j].Y) / 2);
                var l = PixelToLocal(mid);
                if (Math.Abs(p.X - l.X) <= r && Math.Abs(p.Y - l.Y) <= r) return i;
            }
            return -1;
        }

        /// <summary>edge 선분 근처 hit. 삽입 후 새 꼭짓점 인덱스 반환, 아니면 -1.</summary>
        private int HitTestEdgeLine(Point p)
        {
            double z = GetZoom();
            double tol = EdgeHitTolerance / z;
            double tol2 = tol * tol;
            int bestI = -1;
            double bestD2 = tol2;
            for (int i = 0; i < _points.Count; i++)
            {
                int j = (i + 1) % _points.Count;
                var a = PixelToLocal(_points[i]);
                var b = PixelToLocal(_points[j]);
                double d2 = DistanceSquaredToSegment(p, a, b);
                if (d2 <= bestD2) { bestD2 = d2; bestI = i; }
            }
            return bestI;
        }

        private static double DistanceSquaredToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 < 1e-6) return (p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y);
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            double px = a.X + t * dx, py = a.Y + t * dy;
            return (p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py);
        }

        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
        {
            var p = hitTestParameters.HitPoint;
            // Vertex / edge midpoint / edge line 중 하나라도 맞으면 hit.
            if (HitTestVertex(p) >= 0) return new PointHitTestResult(this, p);
            if (HitTestEdgeMidpoint(p) >= 0) return new PointHitTestResult(this, p);
            if (HitTestEdgeLine(p) >= 0) return new PointHitTestResult(this, p);
            return null;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var local = e.GetPosition(this);
            Focus();

            // 1) vertex hit → 드래그.
            int vi = HitTestVertex(local);
            if (vi >= 0)
            {
                _dragVertexIndex = vi;
                CaptureMouse();
                e.Handled = true;
                return;
            }
            // 2) edge midpoint hit → 새 점 삽입 후 드래그.
            int emi = HitTestEdgeMidpoint(local);
            if (emi >= 0)
            {
                int insertAt = emi + 1;
                var px = ClampPixel(LocalToPixel(local));
                _points.Insert(insertAt, px);
                _dragVertexIndex = insertAt;
                CaptureMouse();
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            // 3) edge line 근처 hit → 새 점 삽입 후 드래그.
            int eli = HitTestEdgeLine(local);
            if (eli >= 0)
            {
                int insertAt = eli + 1;
                var px = ClampPixel(LocalToPixel(local));
                _points.Insert(insertAt, px);
                _dragVertexIndex = insertAt;
                CaptureMouse();
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragVertexIndex < 0) return;
            var local = e.GetPosition(this);
            var px = ClampPixel(LocalToPixel(local));
            _points[_dragVertexIndex] = px;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragVertexIndex < 0) return;
            _dragVertexIndex = -1;
            ReleaseMouseCapture();
            RaiseCommit();
            e.Handled = true;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            var local = e.GetPosition(this);
            int vi = HitTestVertex(local);
            if (vi >= 0 && _points.Count > 3)
            {
                _points.RemoveAt(vi);
                InvalidateVisual();
                RaiseCommit();
                e.Handled = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                var mousePos = Mouse.GetPosition(this);
                int vi = HitTestVertex(mousePos);
                if (vi >= 0 && _points.Count > 3)
                {
                    _points.RemoveAt(vi);
                    InvalidateVisual();
                    RaiseCommit();
                    e.Handled = true;
                }
            }
        }

        private void RaiseCommit()
        {
            CommitRequested?.Invoke(this,
                new PolygonVertexCommitEventArgs(_instance.Id, new List<Point>(_points)));
        }
    }

    public sealed class PolygonVertexCommitEventArgs : EventArgs
    {
        public PolygonVertexCommitEventArgs(uint instanceId, IReadOnlyList<Point> points)
        {
            InstanceId = instanceId;
            Points = points;
        }
        public uint InstanceId { get; }
        public IReadOnlyList<Point> Points { get; }
    }
}
