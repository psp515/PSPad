using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Reading;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Reading;

[UnitTest]
public class MoneySummaryTests
{
    static readonly YearMonth January = new(2026, 1);
    static readonly YearMonth February = new(2026, 2);
    static readonly YearMonth March = new(2026, 3);
    static readonly YearMonth April = new(2026, 4);
    static readonly DateOnly YearStart = new(2026, 1, 1);
    static readonly DateOnly YearEnd = new(2026, 12, 31);

    static DateOnly On(int month, int day) => new(2026, month, day);

    static decimal[] PerMonth(MoneySummary summary, string category) =>
        [.. summary.ExpensesByMonth.Single(series => series.Category == category).PerMonth];

    static MoneyEntry[] SixCategories(Budget budget) =>
    [
        AnExpense(budget, "Home", 500m, On(1, 3)),
        AnExpense(budget, "Food", 400m, On(2, 3)),
        AnExpense(budget, "Car", 300m, On(1, 4)),
        AnExpense(budget, "PC", 200m, On(2, 4)),
        AnExpense(budget, "Bike", 50m, On(1, 5)),
        AnExpense(budget, "Gifts", 25m, On(2, 5)),
        AnExpense(budget, "Gifts", 5m, On(1, 6))
    ];

    [Fact]
    public void TotalsAreInPlnAndSavedIsIncomeMinusExpenses()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
        [
            AnExpense(budget, amount: 100m, date: On(3, 2)),
            AnExpense(budget, date: On(3, 3), money: new Money(10m, "EUR", 4.3m, On(3, 3))),
            AnIncome(budget, amount: 1000m, date: On(3, 1))
        ], [], YearStart, YearEnd);

        Assert.Equal(1000m, summary.Income);
        Assert.Equal(143m, summary.Expenses);
        Assert.Equal(857m, summary.Saved);
        Assert.Equal(0.857m, summary.SavingsRate);
    }

    [Fact]
    public void WithoutIncomeThereIsNoSavingsRate()
    {
        var budget = ABudget();

        var summary = MoneySummary.For([AnExpense(budget, amount: 50m, date: On(3, 2))], [], YearStart, YearEnd);

        Assert.Equal(0m, summary.Income);
        Assert.Equal(-50m, summary.Saved);
        Assert.Null(summary.SavingsRate);
    }

    [Fact]
    public void SpendingMoreThanTheIncomeIsANegativeSavingsRate()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
            [AnIncome(budget, amount: 200m, date: On(5, 1)), AnExpense(budget, amount: 300m, date: On(5, 2))],
            [], YearStart, YearEnd);

        Assert.Equal(-0.5m, summary.SavingsRate);
    }

    [Fact]
    public void AnEmptyRangeHasZeroTotalsAndNothingToRank()
    {
        var summary = MoneySummary.For([], [], YearStart, YearEnd);

        Assert.Equal(0m, summary.Saved);
        Assert.Null(summary.SavingsRate);
        Assert.Empty(summary.Ranking);
        Assert.Empty(summary.ExpensesByMonth);
        Assert.Equal(12, summary.Cumulative.Count);
        Assert.Null(summary.Latest);
        Assert.Null(summary.LatestNetWorth);
    }

    [Fact]
    public void BothEndsOfTheRangeCountAndNothingOutside()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
        [
            AnExpense(budget, amount: 1m, date: On(3, 14)),
            AnExpense(budget, amount: 2m, date: On(3, 15)),
            AnExpense(budget, amount: 4m, date: On(4, 10)),
            AnExpense(budget, amount: 8m, date: On(4, 11))
        ], [], On(3, 15), On(4, 10));

        Assert.Equal(6m, summary.Expenses);
        Assert.Equal([new CumulativeMonth(March, 0m, 2m), new CumulativeMonth(April, 0m, 6m)], summary.Cumulative);
    }

    [Fact]
    public void ADayEitherSideOfNewYearLandsInItsOwnYear()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
            [AnExpense(budget, amount: 1m, date: new DateOnly(2025, 12, 31)), AnExpense(budget, amount: 2m, date: YearStart)],
            [], YearStart, On(10, 9));

        Assert.Equal(2m, summary.Expenses);
        Assert.Equal(new CumulativeMonth(January, 0m, 2m), summary.Cumulative[0]);
    }

    [Fact]
    public void DeletedEntriesDoNotCount()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
            [Delete(AnExpense(budget, amount: 50m, date: On(3, 2)), budget), AnExpense(budget, amount: 5m, date: On(3, 2))],
            [], YearStart, YearEnd);

        Assert.Equal(5m, summary.Expenses);
        Assert.Equal([new CategoryTotal("Food", 5m)], summary.Ranking);
    }

    [Fact]
    public void TheMonthsRunFromTheFirstMonthToTheLastAcrossAYearEnd()
    {
        var summary = MoneySummary.For([], [], new DateOnly(2025, 11, 15), new DateOnly(2026, 2, 3));

        Assert.Equal([new YearMonth(2025, 11), new YearMonth(2025, 12), January, February], summary.Months);
        Assert.Equal(summary.Months, summary.Cumulative.Select(month => month.Month));
        Assert.Equal(summary.Months, summary.NetWorth.Select(month => month.Month));
    }

    [Fact]
    public void ASingleDayIsOneMonth() =>
        Assert.Equal([March], MoneySummary.For([], [], On(3, 5), On(3, 5)).Months);

    [Fact]
    public void ARangeThatEndsBeforeItStartsIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MoneySummary.For([], [], On(3, 2), On(3, 1)));

    [Fact]
    public void IncomeAndExpensesAccumulateMonthByMonth()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
        [
            AnIncome(budget, amount: 1000m, date: On(1, 5)),
            AnExpense(budget, amount: 200m, date: On(1, 6)),
            AnIncome(budget, amount: 500m, date: On(3, 1)),
            AnExpense(budget, amount: 300m, date: On(3, 31))
        ], [], YearStart, On(3, 31));

        Assert.Equal(
        [
            new CumulativeMonth(January, 1000m, 200m),
            new CumulativeMonth(February, 1000m, 200m),
            new CumulativeMonth(March, 1500m, 500m)
        ], summary.Cumulative);
    }

    [Fact]
    public void TheTopFourCategoriesKeepTheirOwnSeriesAndTheRestGoIntoOther()
    {
        var summary = MoneySummary.For(SixCategories(ABudget()), [], YearStart, On(2, 28));

        Assert.Equal(["Home", "Food", "Car", "PC", "Other"], summary.ExpensesByMonth.Select(series => series.Category));
        Assert.Equal([500m, 0m], PerMonth(summary, "Home"));
        Assert.Equal([0m, 400m], PerMonth(summary, "Food"));
        Assert.Equal([300m, 0m], PerMonth(summary, "Car"));
        Assert.Equal([0m, 200m], PerMonth(summary, "PC"));
        Assert.Equal([55m, 25m], PerMonth(summary, "Other"));
    }

    [Fact]
    public void FourCategoriesNeedNoOther()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
        [
            AnExpense(budget, "Home", 4m, On(1, 3)),
            AnExpense(budget, "Food", 3m, On(1, 3)),
            AnExpense(budget, "Car", 2m, On(1, 3)),
            AnExpense(budget, "PC", 1m, On(1, 3))
        ], [], YearStart, On(1, 31));

        Assert.Equal(["Home", "Food", "Car", "PC"], summary.ExpensesByMonth.Select(series => series.Category));
    }

    [Fact]
    public void ACategoryNamedOtherInTheTopFourTakesTheRest()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
        [
            AnExpense(budget, "Other", 500m, On(1, 3)),
            AnExpense(budget, "Home", 400m, On(1, 3)),
            AnExpense(budget, "Food", 300m, On(1, 3)),
            AnExpense(budget, "Car", 200m, On(1, 3)),
            AnExpense(budget, "PC", 100m, On(1, 3)),
            AnExpense(budget, "Bike", 10m, On(1, 3))
        ], [], YearStart, On(1, 31));

        Assert.Equal(["Other", "Home", "Food", "Car"], summary.ExpensesByMonth.Select(series => series.Category));
        Assert.Equal([610m], PerMonth(summary, "Other"));
    }

    [Fact]
    public void ALowercaseOtherInTheTopFourTakesTheRest()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
        [
            AnExpense(budget, "other", 500m, On(1, 3)),
            AnExpense(budget, "Home", 400m, On(1, 3)),
            AnExpense(budget, "Food", 300m, On(1, 3)),
            AnExpense(budget, "Car", 200m, On(1, 3)),
            AnExpense(budget, "PC", 100m, On(1, 3)),
            AnExpense(budget, "Bike", 10m, On(1, 3))
        ], [], YearStart, On(1, 31));

        Assert.Equal(["other", "Home", "Food", "Car"], summary.ExpensesByMonth.Select(series => series.Category));
        Assert.Equal([610m], PerMonth(summary, "other"));
    }

    [Fact]
    public void TheRankingListsEveryCategoryLargestFirstWithoutOther()
    {
        var summary = MoneySummary.For(SixCategories(ABudget()), [], YearStart, On(2, 28));

        Assert.Equal(
        [
            new CategoryTotal("Home", 500m),
            new CategoryTotal("Food", 400m),
            new CategoryTotal("Car", 300m),
            new CategoryTotal("PC", 200m),
            new CategoryTotal("Bike", 50m),
            new CategoryTotal("Gifts", 30m)
        ], summary.Ranking);
    }

    [Fact]
    public void RankingTiesAreOrderedByName()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
            [AnExpense(budget, "Car", 5m, On(1, 3)), AnExpense(budget, "Bike", 5m, On(1, 4))], [], YearStart, On(1, 31));

        Assert.Equal([new CategoryTotal("Bike", 5m), new CategoryTotal("Car", 5m)], summary.Ranking);
    }

    [Fact]
    public void IncomeIsNotRanked()
    {
        var budget = ABudget();

        var summary = MoneySummary.For([AnIncome(budget, amount: 900m, date: On(1, 3))], [], YearStart, On(1, 31));

        Assert.Empty(summary.Ranking);
        Assert.Empty(summary.ExpensesByMonth);
    }

    [Fact]
    public void NetWorthIsSplitByHoldingTypeAtTheSnapshotsRates()
    {
        var budget = ABudget();
        var january = ASnapshot(budget, January,
        [
            AHolding("Wallet", 100m),
            AHolding("Spare", 50m),
            AHolding("Konto", 10m, "EUR", 4.30m, type: HoldingType.Savings),
            AHolding("Broker", 5m, "EUR", 4.20m, type: HoldingType.Investment)
        ]);

        var summary = MoneySummary.For([], [january], YearStart, On(1, 31));

        var month = Assert.Single(summary.NetWorth);
        Assert.Equal(new NetWorthMonth(January, 150m, 43m, 21m), month);
        Assert.Equal(214m, month.Total);
        Assert.False(month.IsGap);
    }

    [Fact]
    public void AMonthWithOnlyCashHasZeroForTheOtherTypes()
    {
        var budget = ABudget();

        var summary = MoneySummary.For([], [ASnapshot(budget, January, [AHolding("Wallet", 100m)])], YearStart, On(1, 31));

        Assert.Equal(new NetWorthMonth(January, 100m, 0m, 0m), Assert.Single(summary.NetWorth));
    }

    [Fact]
    public void AMonthWithoutALiveSnapshotIsAGapNotCarriedForward()
    {
        var budget = ABudget();
        var january = ASnapshot(budget, January, [AHolding("Wallet", 100m)]);
        var february = DeleteSnapshot(ASnapshot(budget, February, [AHolding("Wallet", 999m)]), budget);
        var april = ASnapshot(budget, April, [AHolding("Wallet", 300m)]);

        var summary = MoneySummary.For([], [april, february, january], YearStart, On(4, 30));

        Assert.Equal(
        [
            new NetWorthMonth(January, 100m, 0m, 0m),
            new NetWorthMonth(February, null, null, null),
            new NetWorthMonth(March, null, null, null),
            new NetWorthMonth(April, 300m, 0m, 0m)
        ], summary.NetWorth);
        Assert.True(summary.NetWorth[2].IsGap);
        Assert.Null(summary.NetWorth[2].Total);
    }

    [Fact]
    public void LatestNetWorthIsTheNewestLiveSnapshotOnOrBeforeTheRangeEnd()
    {
        var budget = ABudget();
        var lastAugust = ASnapshot(budget, new YearMonth(2025, 8), [AHolding("Wallet", 80m)]);
        var september = DeleteSnapshot(ASnapshot(budget, new YearMonth(2026, 9), [AHolding("Wallet", 500m)]), budget);
        var november = ASnapshot(budget, November, [AHolding("Wallet", 900m)]);

        var summary = MoneySummary.For([], [november, september, lastAugust], YearStart, On(10, 9));

        Assert.Same(lastAugust, summary.Latest);
        Assert.Equal(80m, summary.LatestNetWorth);
    }

    [Fact]
    public void ASnapshotInTheRangesLastMonthIsTheLatest()
    {
        var budget = ABudget();
        var october = ASnapshot(budget, October, [AHolding("Wallet", 120m)]);

        var summary = MoneySummary.For([], [october], YearStart, On(10, 9));

        Assert.Same(october, summary.Latest);
        Assert.Equal(120m, summary.LatestNetWorth);
    }

    [Fact]
    public void PerMonthCarriesEveryMonthOfTheRangeWithItsTotals()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
        [
            AnIncome(budget, amount: 1000m, date: On(1, 5)),
            AnExpense(budget, amount: 200m, date: On(1, 6)),
            AnExpense(budget, amount: 50m, date: On(3, 2))
        ], [], YearStart, new DateOnly(2026, 3, 31));

        Assert.Equal([January, February, March], summary.PerMonth.Select(month => month.Month));
        Assert.Equal([1000m, 0m, 0m], summary.PerMonth.Select(month => month.Income));
        Assert.Equal([200m, 0m, 50m], summary.PerMonth.Select(month => month.Expenses));
        Assert.Equal(800m, summary.PerMonth[0].Net);
    }

    [Fact]
    public void AveragesDivideByTheMonthsThatHaveEntries()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
        [
            AnIncome(budget, amount: 1000m, date: On(1, 5)),
            AnExpense(budget, amount: 200m, date: On(1, 6)),
            AnExpense(budget, amount: 100m, date: On(3, 2))
        ], [], YearStart, new DateOnly(2026, 3, 31));

        Assert.Equal(2, summary.SampledMonths);
        Assert.Equal(500m, summary.AverageIncome);
        Assert.Equal(150m, summary.AverageExpenses);
        Assert.Equal(350m, summary.AverageSaved);
    }

    [Fact]
    public void AveragesRoundToWholeGrosze()
    {
        var budget = ABudget();

        var summary = MoneySummary.For(
            [AnExpense(budget, amount: 10m, date: On(1, 6)), AnExpense(budget, amount: 0.01m, date: On(2, 6)), AnExpense(budget, amount: 0m + 0.01m, date: On(3, 6))],
            [], YearStart, new DateOnly(2026, 3, 31));

        Assert.Equal(3.34m, summary.AverageExpenses);
    }

    [Fact]
    public void WithoutEntriesThereAreNoAverages()
    {
        var summary = MoneySummary.For([], [], YearStart, YearEnd);

        Assert.Equal(0, summary.SampledMonths);
        Assert.Null(summary.AverageIncome);
        Assert.Null(summary.AverageExpenses);
        Assert.Null(summary.AverageSaved);
    }
}
