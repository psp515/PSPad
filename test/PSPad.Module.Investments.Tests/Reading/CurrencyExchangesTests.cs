using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Reading.Market;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class CurrencyExchangesTests
{
    const string PlnNumber = "50000001";
    const string UsdNumber = "50000002";

    static readonly FxBook Today = Rates(("USD", Day(1), 3.80m), ("USD", Day(20), 3.65m), ("EUR", Day(20), 4.30m));

    static InvestmentAccount PlnAccount() => AnAccount("PLN", number: PlnNumber);

    static InvestmentAccount Usd() => AnAccount("USD", number: UsdNumber);

    static InvestmentOperation PlnToUsd(InvestmentAccount pln, decimal plnAmount, decimal usd, DateOnly day) =>
        TransferOut(pln, TransferType.CurrencyConversion, plnAmount, day, UsdNumber, Foreign(usd, "USD", plnAmount / usd, day));

    static InvestmentOperation UsdFromPln(InvestmentAccount account, decimal usd, decimal plnAmount, DateOnly day) =>
        TransferIn(account, TransferType.CurrencyConversion, usd, day, PlnNumber, Pln(plnAmount, day), rate: 3.8m);

    static InvestmentOperation UsdToPln(InvestmentAccount account, decimal usd, decimal plnAmount, DateOnly day) =>
        TransferOut(account, TransferType.CurrencyConversion, usd, day, PlnNumber, Pln(plnAmount, day), rate: 3.8m);

    static IReadOnlyList<CurrencyExchange> Read(
        IReadOnlyList<InvestmentAccount> accounts, IReadOnlyList<InvestmentOperation> operations, Guid? only = null) =>
        CurrencyExchanges.For(accounts, operations, Today, Day(20), Utc, only);

    [Fact]
    public void BoughtAndSoldBackAverageThePlnPaidAndReceived()
    {
        var usd = Usd();
        var operations = new[] {
            UsdFromPln(usd, 1000m, 3900m, Day(1)),
            UsdFromPln(usd, 1500m, 5900m, Day(3)),
            UsdToPln(usd, 100m, 400m, Day(9)),
        };

        var row = Assert.Single(Read([usd], operations));

        Assert.Equal("USD", row.Currency);
        Assert.Equal((2500m, 9800m, 3.92m), (row.BoughtUnits, row.BoughtPln, row.BoughtRate));
        Assert.Equal((100m, 400m, 4m), (row.SoldUnits, row.SoldPln, row.SoldRate));
        Assert.Equal(3, row.Count);
        Assert.Equal((Day(1), Day(9)), (row.First, row.Last));
    }

    [Fact]
    public void PairedLegsCountOnce()
    {
        var pln = PlnAccount();
        var usd = Usd();
        var operations = new[] { PlnToUsd(pln, 3900m, 1000m, Day(1)), UsdFromPln(usd, 1000m, 3900m, Day(1)) };

        var row = Assert.Single(Read([pln, usd], operations));

        Assert.Equal((1000m, 3900m, 1), (row.BoughtUnits, row.BoughtPln, row.Count));
    }

    [Fact]
    public void OnlyThePlnSideImportedStillCountsWithNothingHeld()
    {
        var pln = PlnAccount();

        var row = Assert.Single(Read([pln], [PlnToUsd(pln, 3900m, 1000m, Day(1))]));

        Assert.Equal((1000m, 3900m), (row.BoughtUnits, row.BoughtPln));
        Assert.Null(row.HeldUnits);
        Assert.Null(row.CurrencyResult);
    }

    [Fact]
    public void OnlyTheCurrencySideImportedCounts()
    {
        var usd = Usd();

        var row = Assert.Single(Read([usd], [UsdFromPln(usd, 1000m, 3900m, Day(1))]));

        Assert.Equal((1000m, 3900m, 1), (row.BoughtUnits, row.BoughtPln, row.Count));
    }

    [Fact]
    public void ADeletedAccountIsIgnored()
    {
        var usd = Usd();
        var bought = UsdFromPln(usd, 1000m, 3900m, Day(1));
        usd.ApplyAll(InvestmentAccount.Decide(usd, new DeleteInvestmentAccount(Guid.NewGuid(), Owner, usd.Id), Now));

        Assert.Empty(Read([usd], [bought]));
    }

    [Fact]
    public void HeldUnitsAndResultComeFromTheFxCostPool()
    {
        var usd = Usd();
        var operations = new[] {
            UsdFromPln(usd, 1000m, 3900m, Day(1)),
            UsdFromPln(usd, 1500m, 5900m, Day(3)),
            UsdToPln(usd, 100m, 400m, Day(9)),
        };

        var row = Assert.Single(Read([usd], operations));

        Assert.Equal(2400m, row.HeldUnits);
        Assert.Equal(9408m, row.HeldPln);
        Assert.Equal(3.65m, row.TodayRate);
        Assert.Equal(2400m * 3.65m - 9408m, row.CurrencyResult);
    }

    [Fact]
    public void AForeignAccountWithAnEmptyPoolHasNothingLeftFromConversions()
    {
        var pln = PlnAccount();
        var usd = Usd();
        var operations = new[] { PlnToUsd(pln, 3900m, 1000m, Day(1)), Deposit(usd, 50m, Day(2)) };

        var row = Assert.Single(Read([pln, usd], operations));

        Assert.Null(row.HeldUnits);
        Assert.Null(row.HeldPln);
        Assert.Null(row.CurrencyResult);
    }

    [Fact]
    public void AConversionBetweenTwoForeignCurrenciesAddsUnitsButNoPln()
    {
        var usd = Usd();
        var operations = new[] {
            UsdFromPln(usd, 1000m, 3900m, Day(1)),
            TransferIn(usd, TransferType.CurrencyConversion, 110m, Day(2), null, Foreign(100m, "EUR", 4.3m, Day(2)), rate: 3.8m),
        };

        var row = Assert.Single(Read([usd], operations), exchange => exchange.Currency == "USD");

        Assert.Equal((1110m, 3900m, 3.9m), (row.BoughtUnits, row.BoughtPln, row.BoughtRate));
    }

    [Fact]
    public void APlnAccountViewShowsOnlyItsOwnConversions()
    {
        var pln = PlnAccount();
        var other = AnAccount("PLN", number: "50000009");
        var operations = new[] {
            PlnToUsd(pln, 3900m, 1000m, Day(1)),
            TransferOut(other, TransferType.CurrencyConversion, 430m, Day(1), null, Foreign(100m, "EUR", 4.3m, Day(1))),
        };

        var rows = Read([pln, other], operations, pln.Id);

        Assert.Equal(["USD"], rows.Select(row => row.Currency));
    }
}
