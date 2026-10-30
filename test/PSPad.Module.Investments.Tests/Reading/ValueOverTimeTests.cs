using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Reading.Market;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class ValueOverTimeTests
{
    [Fact]
    public void EachDayValuesTheLotsAtTheCloseOnOrBeforeItAndAtCostBeforeTheFirstClose()
    {
        var account = AnAccount();
        var operations = new[] {
            Deposit(account, 1000m, Day(1)),
            Buy(account, "DEF.PL", position: "A", quantity: 10m, price: 50m, amount: 500m, Day(2)),
        };
        var prices = History("DEF.PL", "PLN", (Day(3), 60m), (Day(5), 55m));

        var points = ValueOverTime.For(account, operations, prices, PlnOnly, Day(1), Day(5), Utc);

        Assert.Equal([
            new ValuePoint(Day(1), 1000m, 1000m),
            new ValuePoint(Day(2), 1000m, 1000m),
            new ValuePoint(Day(3), 1100m, 1000m),
            new ValuePoint(Day(4), 1100m, 1000m),
            new ValuePoint(Day(5), 1050m, 1000m),
        ], points);
    }

    [Fact]
    public void AYearIsStillShownDayByDay()
    {
        var account = AnAccount();

        var points = ValueOverTime.For(account, [Deposit(account, 10m, new DateOnly(2025, 1, 1))], Prices(), PlnOnly,
            new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 2), Utc);

        Assert.Equal(367, points.Count);
    }

    [Fact]
    public void MoreThanAYearIsShownOnePointPerWeekAndTheLastDay()
    {
        var account = AnAccount();
        var to = new DateOnly(2026, 3, 4);

        var points = ValueOverTime.For(account, [Deposit(account, 10m, new DateOnly(2025, 1, 1))], Prices(), PlnOnly,
            new DateOnly(2025, 1, 1), to, Utc);

        Assert.Equal(62, points.Count);
        Assert.Equal(new DateOnly(2025, 1, 5), points[0].Day);
        Assert.All(points.SkipLast(1), point => Assert.Equal(DayOfWeek.Sunday, point.Day.DayOfWeek));
        Assert.Equal(new DateOnly(2026, 3, 1), points[^2].Day);
        Assert.Equal(to, points[^1].Day);
        Assert.All(points, point => Assert.Equal(10m, point.Value));
    }

    [Fact]
    public void AllAccountsAreSummedInPlnAtEachDaysRate()
    {
        var pln = AnAccount();
        var usd = AnAccount("USD");
        var fx = Rates(("USD", Day(1), 3.80m), ("USD", Day(2), 4.00m));
        var plnPoints = ValueOverTime.For(pln, [Deposit(pln, 100m, Day(1))], Prices(), fx, Day(1), Day(2), Utc);
        var usdPoints = ValueOverTime.For(usd, [Deposit(usd, 10m, Day(1), rate: 3.80m)], Prices(), fx, Day(1), Day(2), Utc);

        var points = ValueOverTime.InPln([(pln, plnPoints), (usd, usdPoints)], fx);

        Assert.Equal([new ValuePoint(Day(1), 138m, 138m), new ValuePoint(Day(2), 140m, 140m)], points);
    }

    [Fact]
    public void AnOperationJoinsTheSeriesOnTheDayOfTheUsersZone()
    {
        var account = AnAccount();

        var points = ValueOverTime.For(account, [DepositAt(account, 500m, HalfPastMidnightInWarsaw)], Prices(), PlnOnly,
            LastOfOctober, FirstOfNovember, Warsaw);

        Assert.Equal([new ValuePoint(LastOfOctober, 0m, 0m), new ValuePoint(FirstOfNovember, 500m, 500m)], points);
    }
}
