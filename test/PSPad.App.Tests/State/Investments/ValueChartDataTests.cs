using PSPad.App.Resources;
using PSPad.App.State.Investments;
using PSPad.App.Tests.Localization;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class ValueChartDataTests
{
    static readonly DateOnly Day = new(2026, 10, 1);

    static readonly IReadOnlyList<ValuePoint> Points =
    [
        new(Day, 10_000m, 10_000m),
        new(Day.AddDays(1), 9_400m, 10_000m),
        new(Day.AddDays(2), 12_500m, 11_000m),
    ];

    [Fact]
    public void TheValueViewDrawsValueAndNetDeposits()
    {
        var data = ValueChartData.For(Points, ValueChartMode.Value);

        Assert.Equal(["Value", "NetDeposits"], data.Series.Select(series => series.Key));
        Assert.Equal([10_000d, 9_400d, 12_500d], data.Series[0].Data);
        Assert.Equal([10_000d, 10_000d, 11_000d], data.Series[1].Data);
        Assert.Equal(5_000, data.YStep);
    }

    [Fact]
    public void TheProfitViewDrawsValueLessNetDepositsOnItsOwnScale()
    {
        var data = ValueChartData.For(Points, ValueChartMode.Profit);

        var profit = Assert.Single(data.Series);
        Assert.Equal("Profit", profit.Key);
        Assert.Equal([0d, -600d, 1_500d], profit.Data);
        Assert.Equal(500, data.YStep);
    }

    [Theory]
    [InlineData("en-US", 40_000d, "40k")]
    [InlineData("en-US", 1_500_000d, "1.5M")]
    [InlineData("en-US", -2_500d, "−2.5k")]
    [InlineData("en-US", 0d, "0")]
    [InlineData("en-US", 750d, "750")]
    [InlineData("pl-PL", 40_000d, "40 tys.")]
    [InlineData("pl-PL", 1_500_000d, "1,5 mln")]
    public void AxisLabelsAreCompact(string culture, double value, string label)
    {
        using var scope = new CultureScope(culture);

        Assert.Equal(label, ValueChartData.Compact(value, TestLocalizers.For<InvestmentsStrings>()));
    }
}
