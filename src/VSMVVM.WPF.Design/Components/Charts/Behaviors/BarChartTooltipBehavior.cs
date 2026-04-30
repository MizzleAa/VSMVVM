using System.Windows.Controls;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts.Behaviors
{
    public class BarChartTooltipBehavior : ChartTooltipBehavior
    {
        protected override string FormatBody(ChartHoverState e)
        {
            if (AssociatedObject is BarChart bar && bar.Orientation == Orientation.Horizontal)
            {
                return $"Value: {e.DataX:0.##}    Index: {(int)e.DataY}";
            }
            return $"Index: {(int)e.DataX}    Value: {e.DataY:0.##}";
        }
    }
}
