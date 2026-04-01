using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;

namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 타원 그리기 도구. 기본 자유 비율, Shift=정원.
    /// RectangleTool의 바운딩 박스 로직을 재사용합니다.
    /// </summary>
    public class EllipseTool : RectangleTool
    {
        public override CanvasToolMode Mode => CanvasToolMode.Ellipse;

        protected override Shape CreateFinalShape(Rect bounds)
        {
            return new Ellipse
            {
                Width = bounds.Width,
                Height = bounds.Height,
                Stroke = StrokeColor.Clone(),
                StrokeThickness = StrokeThickness,
                Fill = GetFillBrush(),
            };
        }
    }
}
