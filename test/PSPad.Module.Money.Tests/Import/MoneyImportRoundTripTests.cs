using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Import;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Import;

[UnitTest]
public class MoneyImportRoundTripTests
{
    [Fact]
    public void RunningThePlanBuildsWhatThePreviewPromised()
    {
        var budget = ABudget();
        var august = new YearMonth(2026, 8);
        var wallet = new Holding(Guid.NewGuid(), "Wallet", HoldingType.Cash, new Money(10m, "PLN", 1m, august.LastDay));
        var old = new Holding(Guid.NewGuid(), "Old", HoldingType.Savings, new Money(5m, "PLN", 1m, august.LastDay));
        var runner = new PlanRunner(budget);
        runner.Snapshots.Add(ASnapshot(budget, august, [wallet, old]));
        var preview = MoneyImportPreview.Build(
            MoneyCsv.ParseEntries("kind,date,name,category,amount\nexpense,2026-09-03,Bread,Hobby,4\n"),
            MoneyCsv.ParseBalances("month,holding,type,amount\n2026-09,wallet,cash,20\n2026-09,ETF,investment,7\n" +
                                   "2026-10,ETF,investment,8\n"),
            new ImportContext(budget, [], runner.Snapshots, "PLN"), RateBook.Empty, Guid.NewGuid);

        runner.Run(MoneyImportPlan.Build(preview, new HashSet<int>(), Owner, Guid.NewGuid).Select(planned => planned.Command));

        foreach (var month in preview.Months)
        {
            var snapshot = runner.Snapshots.Single(snapshot => snapshot.Month == month.Month && !snapshot.Deleted);
            Assert.Equal(month.Holdings, snapshot.Holdings.Count);
        }

        var september = runner.Snapshots.Single(snapshot => snapshot.Month == new YearMonth(2026, 9));
        Assert.Equal(["wallet", "ETF"], september.Holdings.Select(holding => holding.Name));
        Assert.Equal(wallet.HoldingId, september.Holdings[0].HoldingId);
        var october = runner.Snapshots.Single(snapshot => snapshot.Month == new YearMonth(2026, 10));
        Assert.Equal(["ETF"], october.Holdings.Select(holding => holding.Name));
        Assert.Equal(8m, october.Holdings[0].Money.Amount);
        Assert.Contains("Hobby", budget.ExpenseCategories);
        Assert.Equal("Bread", Assert.Single(runner.Entries).Name);
    }
}
