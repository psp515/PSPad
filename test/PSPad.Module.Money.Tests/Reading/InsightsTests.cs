using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Reading;

[UnitTest]
public class InsightsTests
{
    static DateOnly On(int month, int day) => new(2026, month, day);

    static IReadOnlyList<Insight> At(DateOnly today, params MoneyEntry[] entries) =>
        Insights.For(MonthPace.For(entries, October, today));

    static MoneyEntry[] Baseline(Budget budget) =>
    [
        AnExpense(budget, "Food", 620m, On(8, 3)),
        AnExpense(budget, "Food", 620m, On(9, 3)),
        AnExpense(budget, "Rent", 2000m, On(8, 1)),
        AnExpense(budget, "Rent", 2000m, On(9, 1)),
        AnIncome(budget, "Salary", 3000m, On(8, 28)),
        AnIncome(budget, "Salary", 3000m, On(9, 28))
    ];

    [Fact]
    public void WithoutABaselineThereAreNoInsights()
    {
        var budget = ABudget();

        Assert.Empty(At(On(10, 10), AnExpense(budget, "Food", 900m, On(10, 2))));
    }

    [Fact]
    public void SpendingPastTheUsualMonthIsPastUsual()
    {
        var budget = ABudget();

        var insight = Assert.Single(At(On(10, 10), [.. Baseline(budget), AnExpense(budget, "Food", 700m, On(10, 2))]),
            insight => insight.Kind == InsightKind.PastUsual);

        Assert.Equal(("Food", 700m, 620m, 0), (insight.Category, insight.Amount, insight.Usual, insight.Day));
    }

    [Fact]
    public void SpendingWellAheadOfTheProRatedUsualIsRunningHigh()
    {
        var budget = ABudget();

        var insights = At(On(10, 10), [.. Baseline(budget), AnExpense(budget, "Food", 300m, On(10, 2))]);

        var high = Assert.Single(insights, insight => insight.Kind == InsightKind.RunningHigh);
        Assert.Equal(("Food", 300m, 200m), (high.Category, high.Amount, high.Usual));
        Assert.DoesNotContain(insights, insight => insight.Kind == InsightKind.PastUsual);
    }

    [Fact]
    public void ASmallOvershootIsNotRunningHigh()
    {
        var budget = ABudget();

        var insights = At(On(10, 10), [.. Baseline(budget), AnExpense(budget, "Food", 240m, On(10, 2))]);

        Assert.DoesNotContain(insights, insight => insight.Kind == InsightKind.RunningHigh);
    }

    [Fact]
    public void AnOvershootUnderFiftyZlotyIsNotRunningHigh()
    {
        var budget = ABudget();
        var small = new[] { AnExpense(budget, "Coffee", 62m, On(9, 3)), AnExpense(budget, "Rent", 2000m, On(9, 1)) };

        var insights = At(On(10, 10), [.. small, AnExpense(budget, "Coffee", 40m, On(10, 2))]);

        Assert.DoesNotContain(insights, insight => insight.Kind == InsightKind.RunningHigh);
    }

    [Fact]
    public void ARegularIncomeNotYetSeenIsExpectedOnItsTypicalDay()
    {
        var budget = ABudget();

        var insight = Assert.Single(At(On(10, 10), Baseline(budget)), insight => insight.Kind == InsightKind.IncomeExpected);

        Assert.Equal(("Salary", 3000m, 28), (insight.Category, insight.Amount, insight.Day));
    }

    [Fact]
    public void AnIncomeAlreadyRecordedOrPastItsDayIsNotExpected()
    {
        var budget = ABudget();

        Assert.DoesNotContain(At(On(10, 10), [.. Baseline(budget), AnIncome(budget, "Salary", 3000m, On(10, 5))]),
            insight => insight.Kind == InsightKind.IncomeExpected);
        Assert.DoesNotContain(At(On(10, 29), Baseline(budget)), insight => insight.Kind == InsightKind.IncomeExpected);
    }

    [Fact]
    public void AnIncomeSeenInFewerThanHalfTheMonthsIsNotExpected()
    {
        var budget = ABudget();
        var sparse = new[]
        {
            AnIncome(budget, "Bonus", 500m, On(6, 15)),
            AnExpense(budget, "Rent", 1m, On(7, 1)),
            AnExpense(budget, "Rent", 1m, On(8, 1)),
            AnExpense(budget, "Rent", 1m, On(9, 1))
        };

        Assert.DoesNotContain(At(On(10, 10), sparse), insight => insight.Kind == InsightKind.IncomeExpected);
    }

    [Fact]
    public void SpendingCloseToTheProRatedUsualIsOnTrack()
    {
        var budget = ABudget();

        var insight = Assert.Single(At(On(10, 10), [.. Baseline(budget), AnExpense(budget, "Food", 195m, On(10, 2))]),
            insight => insight.Kind == InsightKind.OnTrack);

        Assert.Equal(("Food", 195m, 200m), (insight.Category, insight.Amount, insight.Usual));
    }

    [Fact]
    public void InsightsAreOrderedByKindThenAmountAndCapped()
    {
        var budget = ABudget();
        var entries = new List<MoneyEntry>(Baseline(budget))
        {
            AnExpense(budget, "Rent", 2100m, On(10, 1)),
            AnExpense(budget, "Food", 700m, On(10, 2)),
            AnExpense(budget, "Car", 310m, On(8, 3)),
            AnExpense(budget, "Car", 310m, On(9, 3)),
            AnExpense(budget, "Car", 200m, On(10, 3)),
            AnExpense(budget, "Gifts", 155m, On(8, 3)),
            AnExpense(budget, "Gifts", 155m, On(9, 3)),
            AnExpense(budget, "Gifts", 50m, On(10, 3)),
            AnExpense(budget, "Bike", 93m, On(8, 3)),
            AnExpense(budget, "Bike", 93m, On(9, 3)),
            AnExpense(budget, "Bike", 30m, On(10, 3))
        };

        var insights = At(On(10, 10), [.. entries]);

        Assert.Equal(Insights.Limit, insights.Count);
        Assert.Equal(
            [InsightKind.PastUsual, InsightKind.PastUsual, InsightKind.RunningHigh, InsightKind.IncomeExpected, InsightKind.OnTrack],
            insights.Select(insight => insight.Kind));
        Assert.Equal(["Rent", "Food", "Car", "Salary", "Gifts"], insights.Select(insight => insight.Category));
    }
}
