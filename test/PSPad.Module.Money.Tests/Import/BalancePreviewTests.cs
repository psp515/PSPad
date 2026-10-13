using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Import;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Import;

[UnitTest]
public class BalancePreviewTests
{
    const string Header = "month,holding,type,amount,currency,rate\n";
    static readonly YearMonth August = new(2026, 8);
    static readonly YearMonth September = new(2026, 9);

    static MoneyImportPreview Preview(string rows, Budget budget, IReadOnlyList<BalanceSnapshot>? snapshots = null,
        RateBook? rates = null) =>
        MoneyImportPreview.Build(null, MoneyCsv.ParseBalances(Header + rows),
            new ImportContext(budget, [], snapshots ?? [], "PLN"), rates ?? RateBook.Empty, Guid.NewGuid);

    static Holding Pln(string name, decimal amount, YearMonth month, HoldingType type = HoldingType.Cash) =>
        new(Guid.NewGuid(), name, type, new Money(amount, "PLN", 1m, month.LastDay));

    [Fact]
    public void AMonthWithNoEarlierBalanceAddsEveryRow()
    {
        var month = Assert.Single(Preview("2026-09,Wallet,cash,350,,\n2026-09,ETF,investment,100,EUR,4.2650\n", ABudget()).Months);

        Assert.True(month.IsNew);
        Assert.Equal(2, month.Adds);
        Assert.Equal(2, month.Holdings);
        var etf = month.Changes[1].Holding;
        Assert.Equal(new Money(100m, "EUR", 4.2650m, September.LastDay), etf.Money);
        Assert.NotEqual(Guid.Empty, etf.HoldingId);
    }

    [Fact]
    public void ANewMonthHoldsExactlyTheFileRowsAndKeepsCopiedIds()
    {
        var budget = ABudget();
        var wallet = Pln("Wallet", 100m, August);
        var old = Pln("Old account", 50m, August);
        var august = ASnapshot(budget, August, [wallet, old]);

        var preview = Preview("2026-09,wallet,cash,120,,\n2026-09,ETF,investment,10,,\n", budget, [august]);

        var month = Assert.Single(preview.Months);
        Assert.True(month.IsNew);
        Assert.Equal(2, month.Holdings);
        var edit = Assert.Single(month.Changes, change => change.Kind == HoldingChangeKind.Edit);
        Assert.Equal(wallet.HoldingId, edit.Holding.HoldingId);
        Assert.Equal("wallet", edit.Holding.Name);
        Assert.Equal(old.HoldingId, Assert.Single(month.Changes, change => change.Kind == HoldingChangeKind.Remove).Holding.HoldingId);
        Assert.Equal(1, month.Adds);
    }

    [Fact]
    public void AnExistingMonthMergesByNameAndARespellingCountsAsAnUpdate()
    {
        var budget = ABudget();
        var september = ASnapshot(budget, September, [Pln("Wallet", 100m, September), Pln("Savings", 5m, September, HoldingType.Savings)]);

        var preview = Preview("2026-09,WALLET,cash,100,,\n2026-09,Savings,savings,6,,\n2026-09,ETF,investment,1,,\n", budget, [september]);

        var month = Assert.Single(preview.Months);
        Assert.False(month.IsNew);
        Assert.Equal(2, month.Updates);
        Assert.Equal(1, month.Adds);
        Assert.Equal(3, month.Holdings);
        Assert.DoesNotContain(month.Changes, change => change.Kind == HoldingChangeKind.Remove);
        Assert.Equal([ImportStatus.ToImport, ImportStatus.ToImport, ImportStatus.ToImport],
            preview.Holdings.Select(row => row.Status));
    }

    [Fact]
    public void AnIdenticalHoldingIsUnchangedAndProducesNoChange()
    {
        var budget = ABudget();
        var september = ASnapshot(budget, September, [Pln("Wallet", 100m, September)]);

        var preview = Preview("2026-09,Wallet,cash,100.00,,\n", budget, [september]);

        Assert.Equal(ImportStatus.Unchanged, Assert.Single(preview.Holdings).Status);
        Assert.Empty(Assert.Single(preview.Months).Changes);
        Assert.Equal(1, preview.Months[0].Unchanged);
    }

    [Fact]
    public void MonthsInTheFileCarryIntoLaterMonthsOfTheSameRun()
    {
        var preview = Preview("2026-10,Wallet,cash,30,,\n2026-09,Wallet,cash,20,,\n2026-09,Old,cash,1,,\n", ABudget());

        Assert.Equal([September, new YearMonth(2026, 10)], preview.Months.Select(month => month.Month));
        var septemberWallet = preview.Months[0].Changes.First(change => change.Holding.Name == "Wallet").Holding;
        var october = preview.Months[1];
        Assert.Equal(septemberWallet.HoldingId, Assert.Single(october.Changes, change => change.Kind == HoldingChangeKind.Edit).Holding.HoldingId);
        Assert.Equal("Old", Assert.Single(october.Changes, change => change.Kind == HoldingChangeKind.Remove).Holding.Name);
    }

    [Fact]
    public void ADeletedMonthCountsAsNew()
    {
        var budget = ABudget();
        var deleted = DeleteSnapshot(ASnapshot(budget, September, [Pln("Wallet", 1m, September)]), budget);

        Assert.True(Assert.Single(Preview("2026-09,Wallet,cash,1,,\n", budget, [deleted]).Months).IsNew);
    }

    [Fact]
    public void ANameRepeatedWithinAMonthIsAnError()
    {
        var preview = Preview("2026-09,Wallet,cash,1,,\n2026-09,wallet,cash,2,,\n2026-08,wallet,cash,3,,\n", ABudget());

        var error = Assert.Single(preview.Holdings, row => row.Status == ImportStatus.Error);
        Assert.Equal(3, error.Line);
        Assert.Equal("Holding wallet appears twice in 2026-09.", error.Error);
    }

    [Fact]
    public void RowsPastTheHoldingCapAreErrors()
    {
        var budget = ABudget();
        var existing = ASnapshot(budget, September,
            [.. Enumerable.Range(0, BalanceSnapshot.MaxHoldings - 1).Select(index => Pln($"H{index}", 1m, September))]);

        var preview = Preview("2026-09,New1,cash,1,,\n2026-09,New2,cash,1,,\n2026-09,H0,cash,2,,\n", budget, [existing]);

        Assert.Equal([ImportStatus.ToImport, ImportStatus.Error, ImportStatus.ToImport], preview.Holdings.Select(row => row.Status));
        Assert.Equal($"A month can have at most {BalanceSnapshot.MaxHoldings} holdings.", preview.Holdings[1].Error);
    }

    [Fact]
    public void AForeignHoldingWithoutARateAsksNbpForTheMonthsLastDay()
    {
        var preview = Preview("2026-09,ETF,investment,1,EUR,\n", ABudget());

        Assert.Equal(new RateRequest("EUR", September.LastDay), Assert.Single(preview.MissingRates));
        Assert.Equal(ImportStatus.WaitingForRate, Assert.Single(preview.Holdings).Status);
    }
}
