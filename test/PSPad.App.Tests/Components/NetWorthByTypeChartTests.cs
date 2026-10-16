using Bunit;
using MudBlazor;
using PSPad.App.Components;
using PSPad.Module.Money.Reading;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class NetWorthByTypeChartTests : Bunit.TestContext
{
    static readonly NetWorthMonth[] Months =
    [
        new(new YearMonth(2026, 1), 100m, 40m, 0m),
        new(new YearMonth(2026, 2), null, null, null),
        new(new YearMonth(2026, 3), 0m, 0m, 60m)
    ];

    IRenderedComponent<NetWorthByTypeChart> Chart()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 10, 9));
        return Render<NetWorthByTypeChart>(parameters => parameters
            .Add(p => p.Months, Months)
            .Add(p => p.Labels, ["Jan", "Feb", "Mar"])
            .Add(p => p.Height, "200px")
            .Add(p => p.Caption, "Net worth by holding type")
            .Add(p => p.Class, "pspad-some-chart"));
    }

    [Fact]
    public void AGapMonthDrawsZeroAndReadsAsADash()
    {
        var chart = Chart();

        var mud = chart.FindComponent<MudChart<double>>().Instance;
        Assert.Equal(ChartType.StackedBar, mud.ChartType);
        Assert.Equal("200px", mud.Height);
        Assert.Contains("pspad-some-chart", mud.Class);
        Assert.Equal(["Cash", "Savings", "Investment"], mud.ChartSeries.Select(series => series.Name));
        Assert.Equal([100d, 0d, 0d], mud.ChartSeries[0].Data.Values);
        Assert.Equal(["Month", "Cash", "Savings", "Investment", "Total"],
            chart.FindAll("table.mud-sr-only thead th").Select(cell => cell.TextContent.Trim()));
        Assert.Equal("Net worth by holding type", chart.Find("table.mud-sr-only caption").TextContent.Trim());
        var gap = chart.FindAll("table.mud-sr-only tbody tr")[1].QuerySelectorAll("td").Select(cell => cell.TextContent.Trim());
        Assert.Equal(["February 2026", "—", "—", "—", "—"], gap);
    }

    [Fact]
    public void TheYAxisScalesToTheLargestTotal() =>
        Assert.Equal(50, Assert.IsType<StackedBarChartOptions>(Chart().FindComponent<MudChart<double>>().Instance.ChartOptions).YAxisTicks);
}
