using System.Windows;
using System.Windows.Media;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.WPF.Scheduler.ViewModels;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>
    /// 한 NodeConnection을 큐빅 베지어 곡선으로 렌더링한다.
    /// Start/End 점은 ConnectionViewModel 로부터 바인딩, 색은 핀 Kind 에 따라 흰색(Exec) 또는 데이터 색.
    /// </summary>
    public sealed class ConnectionView : FrameworkElement
    {
        public static readonly DependencyProperty StartProperty =
            DependencyProperty.Register(nameof(Start), typeof(Point), typeof(ConnectionView),
                new FrameworkPropertyMetadata(default(Point), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty EndProperty =
            DependencyProperty.Register(nameof(End), typeof(Point), typeof(ConnectionView),
                new FrameworkPropertyMetadata(default(Point), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeBrushProperty =
            DependencyProperty.Register(nameof(StrokeBrush), typeof(Brush), typeof(ConnectionView),
                new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(ConnectionView),
                new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PinKindProperty =
            DependencyProperty.Register(nameof(PinKind), typeof(PinKind), typeof(ConnectionView),
                new FrameworkPropertyMetadata(PinKind.Exec, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurvatureOffsetProperty =
            DependencyProperty.Register(nameof(CurvatureOffset), typeof(double), typeof(ConnectionView),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ConnectionView),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        /// <summary>
        /// 베지어 곡선이 펴지는 방향. Horizontal 이면 컨트롤 포인트가 수평으로 (좌→우 흐름),
        /// Vertical 이면 수직으로 (위→아래 흐름). NodeGraphCanvas 가 GraphVM.LayoutOrientation 으로 채움.
        /// </summary>
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(GraphLayoutOrientation), typeof(ConnectionView),
                new FrameworkPropertyMetadata(GraphLayoutOrientation.Horizontal,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public GraphLayoutOrientation Orientation
        {
            get => (GraphLayoutOrientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public Point Start
        {
            get => (Point)GetValue(StartProperty);
            set => SetValue(StartProperty, value);
        }

        public Point End
        {
            get => (Point)GetValue(EndProperty);
            set => SetValue(EndProperty, value);
        }

        public Brush StrokeBrush
        {
            get => (Brush)GetValue(StrokeBrushProperty);
            set => SetValue(StrokeBrushProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public PinKind PinKind
        {
            get => (PinKind)GetValue(PinKindProperty);
            set => SetValue(PinKindProperty, value);
        }

        /// <summary>
        /// 베지어 중간 컨트롤 포인트를 수직으로 ±offset px만큼 이동 — 같은 시작/종료 핀에서 나가는 형제 연결들이
        /// 겹치지 않고 부채꼴로 분리되도록 한다. NodeGraphViewModel이 형제 개수 기준으로 자동 계산.
        /// </summary>
        public double CurvatureOffset
        {
            get => (double)GetValue(CurvatureOffsetProperty);
            set => SetValue(CurvatureOffsetProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // 큐빅 베지어. Orientation 에 따라 컨트롤 포인트가 펴지는 방향이 달라진다:
            //   Horizontal: 시작/끝점 X 차이의 절반만큼 수평으로 펴짐 (좌→우 흐름에서 자연스러움).
            //   Vertical:   Y 차이의 절반만큼 수직으로 펴짐 (위→아래 흐름; Netron 스타일).
            // CurvatureOffset 은 형제 연결 분리용 — Horizontal 모드는 Y 로 ±offset, Vertical 모드는 X 로 ±offset.
            Point c1, c2;
            if (Orientation == GraphLayoutOrientation.Vertical)
            {
                var dy = (End.Y - Start.Y) * 0.5;
                var offsetX = CurvatureOffset;
                c1 = new Point(Start.X + offsetX, Start.Y + dy);
                c2 = new Point(End.X + offsetX, End.Y - dy);
            }
            else
            {
                var dx = (End.X - Start.X) * 0.5;
                var offsetY = CurvatureOffset;
                c1 = new Point(Start.X + dx, Start.Y + offsetY);
                c2 = new Point(End.X - dx, End.Y + offsetY);
            }

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(Start, isFilled: false, isClosed: false);
                ctx.BezierTo(c1, c2, End, isStroked: true, isSmoothJoin: true);
            }
            geometry.Freeze();

            // 1) 클릭 히트 영역 — 투명 두꺼운 스트로크로 클릭 잡기 쉽게
            var hitPen = new Pen(Brushes.Transparent, 12.0);
            hitPen.Freeze();
            dc.DrawGeometry(null, hitPen, geometry);

            // 2) 실제 보이는 선 — 선택 시 두께 + 강조 색
            var thickness = IsSelected ? StrokeThickness + 1.5 : StrokeThickness;
            var brush = IsSelected
                ? (TryFindResource("BorderFocus") as Brush ?? Brushes.DeepSkyBlue)
                : (StrokeBrush ?? Brushes.White);
            var pen = new Pen(brush, thickness);
            pen.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }

        /// <summary>ConnectionViewModel을 받아 양 끝점/색을 갱신하는 헬퍼.</summary>
        public void BindFromViewModel(ConnectionViewModel vm, Brush brush)
        {
            if (vm == null) return;
            Start = vm.Start;
            End = vm.End;
            PinKind = vm.SourcePin.Kind;
            StrokeBrush = brush ?? Brushes.White;
        }
    }
}
