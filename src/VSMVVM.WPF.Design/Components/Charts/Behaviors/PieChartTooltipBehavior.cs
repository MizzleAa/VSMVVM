using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts.Behaviors
{
    public class PieChartTooltipBehavior : ChartTooltipBehavior
    {
        protected override string FormatBody(ChartHoverState e)
            => $"Value {e.DataX:0.##} ({e.DataY:0.#}%)";
    }
}
