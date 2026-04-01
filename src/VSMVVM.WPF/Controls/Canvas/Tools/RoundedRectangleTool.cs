using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;

namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 둥근 사각형 그리기 도구. RectangleTool 상속 + CornerRadius 고유 속성.
    /// </summary>
    public class RoundedRectangleTool : RectangleTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.RoundedRectangle;

        private double _cornerRadius = 8.0;

        /// <summary>둥근 모서리 반경.</summary>
        public double CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; OnPropertyChanged(); }
        }

        protected override Shape CreateFinalShape(Rect bounds)
        {
            return new Rectangle
            {
                Width = bounds.Width,
                Height = bounds.Height,
                RadiusX = CornerRadius,
                RadiusY = CornerRadius,
                Stroke = StrokeColor.Clone(),
                StrokeThickness = StrokeThickness,
                Fill = GetFillBrush(),
            };
        }
    }
}
