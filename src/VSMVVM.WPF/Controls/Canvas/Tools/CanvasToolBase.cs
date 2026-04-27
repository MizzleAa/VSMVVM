using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 그리기 도구 공통 베이스.
    /// 각 도구의 스트로크/채우기 속성을 개별 관리합니다.
    /// </summary>
    public abstract class CanvasToolBase : ICanvasTool, INotifyPropertyChanged
    {
        public abstract CanvasToolMode Mode { get; }
        public abstract Cursor ToolCursor { get; }

        private Brush _strokeColor = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
        private Brush _fillColor = Brushes.Transparent;
        private double _strokeThickness = 2.0;
        private double _fillOpacity = 0.3;

        /// <summary>선 색상.</summary>
        public Brush StrokeColor
        {
            get => _strokeColor;
            set { _strokeColor = value; OnPropertyChanged(); }
        }

        /// <summary>내부 채우기 색상. Transparent면 채우기 없음.</summary>
        public Brush FillColor
        {
            get => _fillColor;
            set { _fillColor = value; OnPropertyChanged(); }
        }

        /// <summary>선 두께.</summary>
        public double StrokeThickness
        {
            get => _strokeThickness;
            set { _strokeThickness = value; OnPropertyChanged(); }
        }

        /// <summary>채우기 불투명도 (0.0~1.0).</summary>
        public double FillOpacity
        {
            get => _fillOpacity;
            set { _fillOpacity = value; OnPropertyChanged(); }
        }

        public abstract bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e);
        public abstract void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e);
        public abstract void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e);

        /// <summary>FillColor에 FillOpacity를 적용한 브러시를 반환합니다.</summary>
        protected Brush GetFillBrush()
        {
            if (FillColor == null || FillColor == Brushes.Transparent)
                return Brushes.Transparent;
            var brush = FillColor.Clone();
            brush.Opacity = FillOpacity;
            brush.Freeze();
            return brush;
        }

        /// <summary>미리보기용 반투명 브러시를 반환합니다.</summary>
        protected Brush GetPreviewFillBrush()
        {
            var brush = StrokeColor.Clone();
            brush.Opacity = 0.15;
            brush.Freeze();
            return brush;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
