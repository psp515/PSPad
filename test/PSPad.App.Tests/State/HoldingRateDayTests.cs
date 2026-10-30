using PSPad.App.State;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class HoldingRateDayTests
{
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void WhileTheMonthRunsItIsToday() =>
        Assert.Equal(Today, HoldingRateDay.Of(new YearMonth(2026, 10), Today));

    [Fact]
    public void AnEndedMonthIsDatedItsLastDay() =>
        Assert.Equal(new DateOnly(2026, 9, 30), HoldingRateDay.Of(new YearMonth(2026, 9), Today));

    [Fact]
    public void OnItsLastDayAMonthIsDatedToday()
    {
        var lastDay = new DateOnly(2026, 10, 31);

        Assert.Equal(lastDay, HoldingRateDay.Of(new YearMonth(2026, 10), lastDay));
    }
}
