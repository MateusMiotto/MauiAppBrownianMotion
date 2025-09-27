using Syncfusion.Maui.Toolkit.Charts;

namespace MauiAppBrownianMotion.Views.Controls
{
    public class LegendExt : ChartLegend
    {
        protected override double GetMaximumSizeCoefficient()
        {
            return 0.5;
        }
    }
}
