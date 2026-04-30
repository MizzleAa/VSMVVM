using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts.Behaviors
{
    public class CandleTooltipBehavior : ChartTooltipBehavior
    {
        protected override string FormatTitle(ChartHoverState e)
        {
            if (e.Tag is CandleHoverInfo info && info.Time.HasValue)
            {
                return $"#{info.Index}  {info.Time:yyyy-MM-dd}";
            }
            return e.Tag is CandleHoverInfo info2 ? $"#{info2.Index}" : "Candle";
        }

        protected override string FormatBody(ChartHoverState e)
        {
            if (e.Tag is CandleHoverInfo info)
            {
                return $"O: {info.Open:0.##}  H: {info.High:0.##}\nL: {info.Low:0.##}  C: {info.Close:0.##}";
            }
            return base.FormatBody(e);
        }
    }
}
