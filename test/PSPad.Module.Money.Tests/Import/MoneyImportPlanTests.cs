using PSPad.Abstractions;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Import;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Import;

[UnitTest]
public class MoneyImportPlanTests
{
    static MoneyImportPreview Preview(string entries, string balances, RateBook? rates = null) =>
        MoneyImportPreview.Build(
            entries.Length == 0 ? null : MoneyCsv.ParseEntries("kind,date,name,category,amount,currency,rate,note\n" + entries),
            balances.Length == 0 ? null : MoneyCsv.ParseBalances("month,holding,type,amount,currency,rate\n" + balances),
            new ImportContext(ABudget(), [], [], "PLN"), rates ?? RateBook.Empty, Guid.NewGuid);

    [Fact]
    public void MonthsComeFirstInOrderThenEntriesInFileOrder()
    {
        var preview = Preview("income,2026-09-10,Pay,Salary,10,,,\nexpense,2026-09-03,Bread,Food,1,,,\n",
            "2026-10,Wallet,cash,2,,\n2026-09,Wallet,cash,1,,\n");

        var plan = MoneyImportPlan.Build(preview, new HashSet<int>(), Owner, Guid.NewGuid);

        Assert.Equal(
            [nameof(StartBalanceMonth), nameof(AddHolding), nameof(StartBalanceMonth), nameof(EditHolding),
             nameof(RecordIncome), nameof(RecordExpense)],
            plan.Select(planned => planned.Command.GetType().Name));
        Assert.Equal(new YearMonth(2026, 9), ((StartBalanceMonth)plan[0].Command).Month);
        Assert.Null(plan[0].Line);
        Assert.Equal(2, plan[4].Line);
        Assert.All(plan, planned => Assert.Equal(Owner, planned.Command.UserId));
    }

    [Fact]
    public void TogglesUntickReadyRowsAndTickDuplicates()
    {
        var ready = new EntryPreview(2, null, null, ImportStatus.ToImport, null);
        var duplicate = ready with { Line = 3, Status = ImportStatus.AlreadyInBudget };
        var error = ready with { Line = 4, Status = ImportStatus.Error };

        Assert.True(MoneyImportPlan.Includes(ready, new HashSet<int>()));
        Assert.False(MoneyImportPlan.Includes(ready, new HashSet<int> { 2 }));
        Assert.False(MoneyImportPlan.Includes(duplicate, new HashSet<int>()));
        Assert.True(MoneyImportPlan.Includes(duplicate, new HashSet<int> { 3 }));
        Assert.False(MoneyImportPlan.Includes(error, new HashSet<int> { 4 }));
    }

    [Fact]
    public void AnEntryCarriesItsResolvedMoney()
    {
        var expense = (RecordExpense)Assert.Single(
            MoneyImportPlan.Build(Preview("expense,2026-09-05,Steam,PC,19.99,EUR,4.2731,note\n", ""),
                new HashSet<int>(), Owner, Guid.NewGuid)).Command;

        Assert.Equal(new Money(19.99m, "EUR", 4.2731m, new DateOnly(2026, 9, 5)), expense.Money);
        Assert.Equal("note", expense.Note);
        Assert.NotEqual(expense.CommandId, expense.EntryId);
    }

    [Fact]
    public void PlanningWhileRatesAreLoadingIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => MoneyImportPlan.Build(
            Preview("expense,2026-09-05,Steam,PC,1,EUR,,\n", ""), new HashSet<int>(), Owner, Guid.NewGuid));
    }

    [Fact]
    public void TheOutcomeCountsWhatLandedAndNamesRejectedLines()
    {
        var preview = Preview("expense,2026-09-03,Bread,Food,1,,,\nexpense,2026-09-04,Milk,Food,1,,,\n",
            "2026-09,Wallet,cash,1,,\n");
        var plan = MoneyImportPlan.Build(preview, new HashSet<int>(), Owner, Guid.NewGuid);
        var results = plan.Select((_, index) => index == 3 ? CommandResult.Rejected("nope") : CommandResult.Ok()).ToArray();

        var outcome = MoneyImportOutcome.From(plan, results);

        Assert.Equal(1, outcome.Entries);
        Assert.Equal(1, outcome.Holdings);
        Assert.Equal(1, outcome.Months);
        Assert.Equal(new CsvRowError(3, "nope"), Assert.Single(outcome.Rejected));
    }
}
