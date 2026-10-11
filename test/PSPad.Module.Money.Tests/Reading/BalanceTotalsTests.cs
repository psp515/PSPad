using PSPad.Module.Money.Reading;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Reading;

[UnitTest]
public class BalanceTotalsTests
{
    static readonly YearMonth August = new(2026, 8);
    static readonly YearMonth September = new(2026, 9);

    [Fact]
    public void NetWorthSumsHoldingsInPlnAtTheirFrozenRates()
    {
        var budget = ABudget();
        var october = ASnapshot(budget, October, [AHolding("Wallet", 100m), AHolding("Konto", 10m, "EUR", 4.2512m)]);

        var totals = BalanceTotals.For([october], October);

        Assert.Same(october, totals.Snapshot);
        Assert.Equal(142.51m, totals.NetWorth);
    }

    [Fact]
    public void WithoutASnapshotTheNetWorthIsZeroAndThereIsNoChange()
    {
        var budget = ABudget();

        var totals = BalanceTotals.For([ASnapshot(budget, September, [AHolding()])], October);

        Assert.Null(totals.Snapshot);
        Assert.Equal(0m, totals.NetWorth);
        Assert.Null(totals.Change);
        Assert.NotNull(totals.Previous);
    }

    [Fact]
    public void TheChangeComparesWithTheLatestEarlierLiveSnapshot()
    {
        var budget = ABudget();
        var august = ASnapshot(budget, August, [AHolding("Wallet", 80m)]);
        var september = DeleteSnapshot(ASnapshot(budget, September, [AHolding("Wallet", 500m)]), budget);
        var october = ASnapshot(budget, October, [AHolding("Wallet", 100m)]);
        var november = ASnapshot(budget, November, [AHolding("Wallet", 900m)]);

        var totals = BalanceTotals.For([november, october, september, august], October);

        Assert.Same(august, totals.Previous);
        Assert.Equal(20m, totals.Change);
    }

    [Fact]
    public void WithoutAnEarlierSnapshotThereIsNoChange()
    {
        var budget = ABudget();

        var totals = BalanceTotals.For([ASnapshot(budget, October, [AHolding()])], October);

        Assert.Null(totals.Previous);
        Assert.Null(totals.Change);
    }

    [Fact]
    public void ADeletedMonthIsNoBalance()
    {
        var budget = ABudget();

        Assert.Null(BalanceTotals.For([DeleteSnapshot(ASnapshot(budget, October, [AHolding()]), budget)], October).Snapshot);
    }

    [Fact]
    public void LatestIsTheNewestLiveSnapshotOnOrBeforeAMonth()
    {
        var budget = ABudget();
        var august = ASnapshot(budget, August);
        var september = DeleteSnapshot(ASnapshot(budget, September), budget);
        var november = ASnapshot(budget, November);

        Assert.Same(august, BalanceTotals.Latest([november, september, august], October));
        Assert.Same(november, BalanceTotals.Latest([november, september, august], November));
        Assert.Null(BalanceTotals.Latest([november], October));
    }

    [Fact]
    public void ValueTodayUsesTodaysRateRoundedHalfAwayFromZero()
    {
        var konto = AHolding("Konto", 1.5m, "EUR", 4.2m);
        var rates = new Dictionary<string, decimal> { ["EUR"] = 4.3333m };

        Assert.Equal(6.50m, BalanceTotals.ValueToday(konto, rates));
    }

    [Fact]
    public void APlnHoldingIsWorthItsAmountWithoutARate() =>
        Assert.Equal(100m, BalanceTotals.ValueToday(AHolding("Wallet", 100m), new Dictionary<string, decimal>()));

    [Fact]
    public void AMissingRateHasNoValueToday() =>
        Assert.Null(BalanceTotals.ValueToday(AHolding("Konto", 10m, "USD", 3.9m), new Dictionary<string, decimal> { ["EUR"] = 4.3m }));

    [Fact]
    public void AtRatesSumsEveryHoldingOnceEveryRateIsKnown()
    {
        var budget = ABudget();
        var october = ASnapshot(budget, October,
            [AHolding("Wallet", 100m), AHolding("Konto", 10m, "EUR", 4.30m), AHolding("Broker", 5m, "EUR", 4.20m)]);

        Assert.Equal(166.00m, BalanceTotals.AtRates(october, new Dictionary<string, decimal> { ["EUR"] = 4.40m }));
        Assert.Null(BalanceTotals.AtRates(october, new Dictionary<string, decimal>()));
    }
}
