using PSPad.App.State.Investments;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class ChartScaleTests
{
    static readonly DateOnly Start = new(2026, 1, 1);

    static readonly DailyClose[] Closes =
    [
        new(Start, 40m),
        new(Start.AddDays(10), 60m),
        new(Start.AddDays(20), 50m)
    ];

    readonly ChartScale _scale = ChartScale.For(Closes, 600, 200, 10);

    [Fact]
    public void TheFirstAndLastDayMeetThePlotEdges()
    {
        Assert.Equal(10, _scale.X(Start), 6);
        Assert.Equal(590, _scale.X(Start.AddDays(20)), 6);
        Assert.Equal(300, _scale.X(Start.AddDays(10)), 6);
    }

    [Fact]
    public void TheHighestPriceSitsAtTheTopPaddingAndTheLowestAtTheBottomPadding()
    {
        Assert.Equal(10, _scale.Y(60m), 6);
        Assert.Equal(190, _scale.Y(40m), 6);
        Assert.Equal(100, _scale.Y(50m), 6);
    }

    [Fact]
    public void AFlatSeriesRunsThroughTheMiddle()
    {
        var flat = ChartScale.For([new DailyClose(Start, 5m), new DailyClose(Start.AddDays(1), 5m)], 600, 200, 10);

        Assert.Equal(100, flat.Y(5m), 6);
    }

    [Fact]
    public void ASingleDaySitsAtTheLeftEdge()
    {
        var single = ChartScale.For([new DailyClose(Start, 5m)], 600, 200, 10);

        Assert.Equal(10, single.X(Start), 6);
    }
}
