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
    /// <see cref="MaskInstance.PolygonContours"/> 가 있는 인스턴스의 꼭짓점 편집 Adorner. 외곽 + hole 다중 contour 지원.
    /// 드래그 = 점 이동, edge 중앙 핸들 클릭/드래그 = 점 삽입, 우클릭/Del = 점 삭제 (contour 별 ≥3 점 유지).
    /// MouseUp 마다 <see cref="CommitRequested"/> 발화 → MaskBehavior 가 MaskLayer.RepaintPolygon + Undo push.
    /// </summary>
    public sealed class PolygonVertexAdorner : Adorner
    {
        private readonly MaskLayer _mask;
        private MaskInstance _instance;
        private readonly List<List<Point>> _contours; // 인덱스 0 = 외곽, ≥1 = hole. 편집 세션 내 현재 상태.
        private int _dragContourIdx = -1;
        private int _dragVertexIdx = -1;
        private const double HandleSize = 8.0;
        private const double EdgeHandleSize = 6.0;
        private const double EdgeHitTolerance = 4.0;

        public PolygonVertexAdorner(MaskLayer mask, MaskInstance instance) : base(mask)
        {
            _mask = mask;
            _instance = instance;
            _contours = new List<List<Point>>();
            if (instance.PolygonContours != null)
            {
                foreach (var c in instance.PolygonContours)
                    if (c != null && c.Count >= 3) _contours.Add(new List<Point>(c));
            }
            else if (instance.PolygonPoints != null && instance.PolygonPoints.Count >= 3)
            {
                _contours.Add(new List<Point>(instance.PolygonPoints));
            }
            IsHitTestVisible = true;
            Focusable = true;
        }

        public MaskInstance Instance => _instance;

        /// <summary>마우스 Up 시 발화. 편집된 모든 contour 전달.</summary>
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
            if (_contours.Count == 0 || _contours[0].Count < 3) return;

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

                var outerPen = new Pen(Brushes.Yellow, 1.5 / z);
                outerPen.Freeze();
                var holePen = new Pen(Brushes.Yellow, 1.5 / z) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
                holePen.Freeze();

                // Contour 마다 별도 PathFigure 로 polyline (외곽-hole 사이 가짜 선 방지).
                for (int ci = 0; ci < _contours.Count; ci++)
                {
                    var pts = _contours[ci];
                    if (pts.Count < 3) continue;
                    var figure = new PathFigure { StartPoint = PixelToLocal(pts[0]), IsClosed = true };
                    for (int i = 1; i < pts.Count; i++)
                        figure.Segments.Add(new LineSegment(PixelToLocal(pts[i]), true));
                    var geo = new PathGeometry();
                    geo.Figures.Add(figure);
                    geo.Freeze();
                    dc.DrawGeometry(null, ci == 0 ? outerPen : holePen, geo);
                }

                // Edge 중앙 핸들 (새 점 삽입용) — 작고 반투명.
                var edgeFill = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)); edgeFill.Freeze();
                var edgeBorder = new Pen(Brushes.DarkGray, 1.0 / z); edgeBorder.Freeze();
                foreach (var pts in _contours)
                {
                    if (pts.Count < 3) continue;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        int j = (i + 1) % pts.Count;
                        var mid = new Point((pts[i].X + pts[j].X) / 2, (pts[i].Y + pts[j].Y) / 2);
                        var l = PixelToLocal(mid);
                        dc.DrawRectangle(edgeFill, edgeBorder, new Rect(l.X - eh / 2, l.Y - eh / 2, eh, eh));
                    }
                }

                // Vertex 핸들 — 흰색 사각.
                var vertBorder = new Pen(Brushes.Black, 1.0 / z); vertBorder.Freeze();
                foreach (var pts in _contours)
                {
                    foreach (var p in pts)
                    {
                        var l = PixelToLocal(p);
                        dc.DrawRectangle(Brushes.White, vertBorder, new Rect(l.X - h / 2, l.Y - h / 2, h, h));
                    }
                }
            }
            finally
            {
                dc.Pop();
            }
        }

        /// <summary>Local 점 p 가 vertex 핸들 위인지. true 면 (contourIdx, vertexIdx), 아니면 (-1, -1).</summary>
        private (int ci, int vi) HitTestVertex(Point p)
        {
            double z = GetZoom();
            double r = HandleSize / z;
            for (int ci = 0; ci < _contours.Count; ci++)
            {
                var pts = _contours[ci];
                for (int i = 0; i < pts.Count; i++)
                {
                    var l = PixelToLocal(pts[i]);
                    if (Math.Abs(p.X - l.X) <= r && Math.Abs(p.Y - l.Y) <= r) return (ci, i);
                }
            }
            return (-1, -1);
        }

        /// <summary>Edge 중앙 핸들 hit. (contourIdx, edgeStartIdx) 반환, 없으면 (-1,-1).</summary>
        private (int ci, int ei) HitTestEdgeMidpoint(Point p)
        {
            double z = GetZoom();
            double r = EdgeHandleSize / z;
            for (int ci = 0; ci < _contours.Count; ci++)
            {
                var pts = _contours[ci];
                if (pts.Count < 3) continue;
                for (int i = 0; i < pts.Count; i++)
                {
                    int j = (i + 1) % pts.Count;
                    var mid = new Point((pts[i].X + pts[j].X) / 2, (pts[i].Y + pts[j].Y) / 2);
                    var l = PixelToLocal(mid);
                    if (Math.Abs(p.X - l.X) <= r && Math.Abs(p.Y - l.Y) <= r) return (ci, i);
                }
            }
            return (-1, -1);
        }

        /// <summary>edge 선분 근처 hit. (contourIdx, edgeStartIdx) 반환, 없으면 (-1,-1).</summary>
        private (int ci, int ei) HitTestEdgeLine(Point p)
        {
            double z = GetZoom();
            double tol = EdgeHitTolerance / z;
            double tol2 = tol * tol;
            int bestCi = -1, bestI = -1;
            double bestD2 = tol2;
            for (int ci = 0; ci < _contours.Count; ci++)
            {
                var pts = _contours[ci];
                if (pts.Count < 3) continue;
                for (int i = 0; i < pts.Count; i++)
                {
                    int j = (i + 1) % pts.Count;
                    var a = PixelToLocal(pts[i]);
                    var b = PixelToLocal(pts[j]);
                    double d2 = DistanceSquaredToSegment(p, a, b);
                    if (d2 <= bestD2) { bestD2 = d2; bestCi = ci; bestI = i; }
                }
            }
            return (bestCi, bestI);
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
            if (HitTestVertex(p).ci >= 0) return new PointHitTestResult(this, p);
            if (HitTestEdgeMidpoint(p).ci >= 0) return new PointHitTestResult(this, p);
            if (HitTestEdgeLine(p).ci >= 0) return new PointHitTestResult(this, p);
            return null;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var local = e.GetPosition(this);
            Focus();

            // 1) vertex hit → 드래그.
            var (vci, vi) = HitTestVertex(local);
            if (vci >= 0)
            {
                _dragContourIdx = vci;
                _dragVertexIdx = vi;
                CaptureMouse();
                e.Handled = true;
                return;
            }
            // 2) edge midpoint hit → 새 점 삽입 후 드래그.
            var (mci, mi) = HitTestEdgeMidpoint(local);
            if (mci >= 0)
            {
                int insertAt = mi + 1;
                var px = ClampPixel(LocalToPixel(local));
                _contours[mci].Insert(insertAt, px);
                _dragContourIdx = mci;
                _dragVertexIdx = insertAt;
                CaptureMouse();
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            // 3) edge line 근처 hit → 새 점 삽입 후 드래그.
            var (lci, li) = HitTestEdgeLine(local);
            if (lci >= 0)
            {
                int insertAt = li + 1;
                var px = ClampPixel(LocalToPixel(local));
                _contours[lci].Insert(insertAt, px);
                _dragContourIdx = lci;
                _dragVertexIdx = insertAt;
                CaptureMouse();
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragContourIdx < 0 || _dragVertexIdx < 0) return;
            var local = e.GetPosition(this);
            var px = ClampPixel(LocalToPixel(local));
            _contours[_dragContourIdx][_dragVertexIdx] = px;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragContourIdx < 0) return;
            _dragContourIdx = -1;
            _dragVertexIdx = -1;
            ReleaseMouseCapture();
            RaiseCommit();
            e.Handled = true;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            var local = e.GetPosition(this);
            var (vci, vi) = HitTestVertex(local);
            if (vci < 0) return;
            if (DeleteVertex(vci, vi))
            {
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
                var (vci, vi) = HitTestVertex(mousePos);
                if (vci < 0) return;
                if (DeleteVertex(vci, vi))
                {
                    InvalidateVisual();
                    RaiseCommit();
                    e.Handled = true;
                }
            }
        }

        /// <summary>vertex 삭제. 외곽이 3 점 미만 되면 거부, hole 이 3 점 미만 되면 hole 자체 제거.</summary>
        private bool DeleteVertex(int ci, int vi)
        {
            var pts = _contours[ci];
            if (ci == 0)
            {
                // 외곽: 3 점 미만 되면 삭제 차단.
                if (pts.Count <= 3) return false;
                pts.RemoveAt(vi);
                return true;
            }
            // Hole: 3 점 미만 되면 hole 자체 제거.
            if (pts.Count <= 3)
            {
                _contours.RemoveAt(ci);
                return true;
            }
            pts.RemoveAt(vi);
            return true;
        }

        private void RaiseCommit()
        {
            var snapshot = new List<IReadOnlyList<Point>>(_contours.Count);
            foreach (var c in _contours)
                snapshot.Add(new List<Point>(c));
            CommitRequested?.Invoke(this, new PolygonVertexCommitEventArgs(_instance.Id, snapshot));
        }
    }

    public sealed class PolygonVertexCommitEventArgs : EventArgs
    {
        public PolygonVertexCommitEventArgs(uint instanceId, IReadOnlyList<IReadOnlyList<Point>> contours)
        {
            InstanceId = instanceId;
            Contours = contours;
        }
        public uint InstanceId { get; }
        /// <summary>인덱스 0 = 외곽, ≥1 = hole.</summary>
        public IReadOnlyList<IReadOnlyList<Point>> Contours { get; }
    }
}
