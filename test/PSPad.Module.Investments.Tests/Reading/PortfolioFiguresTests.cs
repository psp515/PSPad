using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Reading.Market;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class PortfolioFiguresTests
{
    static readonly FxBook Today = Rates(("USD", Day(1), 3.80m), ("USD", Day(20), 4.00m));

    static (InvestmentAccount, AccountFigures, IReadOnlyList<InvestmentOperation>) Read(
        InvestmentAccount account, InvestmentOperation[] operations, IReadOnlyDictionary<string, PriceHistory>? prices = null) =>
        (account, AccountFigures.For(account, operations, prices ?? Prices(), Today, Day(20)), operations);

    static void PlnIdentityHolds(PortfolioFigures figures) =>
        Assert.Equal(figures.RealizedGainsPln + figures.RealizedLossesPln + figures.UnrealizedPln + figures.PassiveIncomePln
            - figures.FeesPln + figures.CurrencyDifferencePln, figures.TotalProfitPln);

    static void DepositsAddUp(PortfolioFigures figures) =>
        Assert.Equal(figures.PutInPln - figures.TakenOutPln, figures.NetDepositsPln);

    [Fact]
    public void AUsdAccountShowsWhatTheRateAddedSinceTheMoneyWentIn()
    {
        var account = AnAccount("USD");
        var usd = Read(account, [
            Deposit(account, 1000m, Day(1), rate: 3.80m),
            Buy(account, "AAPL.US", position: "A", quantity: 10m, price: 50m, amount: 500m, Day(2), rate: 3.80m),
        ], Prices(("AAPL.US", "USD", (Day(20), 60m))));

        var figures = PortfolioFigures.For([usd], Today, Day(20));

        Assert.Equal(4400m, figures.ValuePln);
        Assert.Equal(3800m, figures.NetDepositsPln);
        Assert.Equal(600m, figures.TotalProfitPln);
        Assert.Equal(400m, figures.UnrealizedPln);
        Assert.Equal(200m, figures.CurrencyDifferencePln);
        Assert.False(figures.MissingRates);
    }

    [Fact]
    public void RealizedIncomeAndFeesUseEachOperationsFrozenRate()
    {
        var account = AnAccount("USD");
        var usd = Read(account, [
            Deposit(account, 1000m, Day(1), rate: 3.80m),
            Buy(account, "AAPL.US", position: "A", quantity: 10m, price: 50m, amount: 500m, Day(2), rate: 3.80m),
            Sell(account, "AAPL.US", position: "A", quantity: 5m, price: 60m, amount: 300m, Day(5), rate: 3.90m),
            Dividend(account, "AAPL.US", 10m, Day(6), rate: 3.95m),
            Fee(account, 2m, Day(6), rate: 3.95m),
        ], Prices(("AAPL.US", "USD", (Day(20), 60m))));

        var figures = PortfolioFigures.For([usd], Today, Day(20));

        Assert.Equal(195m, figures.RealizedGainsPln);
        Assert.Equal(39.5m, figures.PassiveIncomePln);
        Assert.Equal(7.9m, figures.FeesPln);
        Assert.Equal(200m, figures.UnrealizedPln);
        Assert.Equal(4432m, figures.ValuePln);
        Assert.Equal(632m, figures.TotalProfitPln);
        Assert.Equal(205.4m, figures.CurrencyDifferencePln);
        PlnIdentityHolds(figures);
    }

    [Fact]
    public void APlnAccountHasNoCurrencyDifference()
    {
        var account = AnAccount();
        var pln = Read(account, [
            Deposit(account, 5000m, Day(1)),
            Buy(account, "JKL.PL", position: "B", quantity: 3m, price: 120m, amount: 360m, Day(3)),
            Sell(account, "JKL.PL", position: "B", quantity: 2m, price: 110m, amount: 220m, Day(10)),
            Interest(account, 1m, Day(13)),
        ], Prices(("JKL.PL", "PLN", (Day(20), 130m))));

        var figures = PortfolioFigures.For([pln], Today, Day(20));

        Assert.Equal(-20m, figures.RealizedLossesPln);
        Assert.Equal(0m, figures.CurrencyDifferencePln);
        PlnIdentityHolds(figures);
    }

    [Fact]
    public void AnAccountWithoutTodaysRateIsLeftOutAndFlagged()
    {
        var plnAccount = AnAccount();
        var pln = Read(plnAccount, [Deposit(plnAccount, 500m, Day(1))], Prices());
        var eurAccount = AnAccount("EUR");
        var eur = Read(eurAccount, [Deposit(eurAccount, 100m, Day(1), rate: 4.3m)], Prices());

        var figures = PortfolioFigures.For([pln, eur], Today, Day(20));

        Assert.True(figures.MissingRates);
        Assert.Equal(500m, figures.ValuePln);
        Assert.Equal(500m, figures.NetDepositsPln);
        Assert.Equal(0m, figures.TotalProfitPln);
    }

    [Fact]
    public void AUsdAccountFundedByAConversionCountsThePlnActuallySpent()
    {
        var account = AnAccount("USD");
        var usd = Read(account, [
            TransferIn(account, TransferType.CurrencyConversion, 1000m, Day(1), counter: Pln(3700m, Day(1)), rate: 3.80m),
            Buy(account, "AAPL.US", position: "A", quantity: 10m, price: 50m, amount: 500m, Day(2), rate: 3.80m),
        ], Prices(("AAPL.US", "USD", (Day(20), 60m))));

        var figures = PortfolioFigures.For([usd], Today, Day(20));

        Assert.Equal(4400m, figures.ValuePln);
        Assert.Equal(3700m, figures.PutInPln);
        Assert.Equal(3700m, figures.NetDepositsPln);
        Assert.Equal(700m, figures.TotalProfitPln);
        Assert.Equal(400m, figures.UnrealizedPln);
        Assert.Equal(300m, figures.CurrencyDifferencePln);
        PlnIdentityHolds(figures);
        DepositsAddUp(figures);
    }

    [Fact]
    public void OperationsBetweenConversionsUseTheAverageRateAndAConversionBackTakesUnitsOutAtIt()
    {
        var account = AnAccount("USD");
        var usd = Read(account, [
            TransferIn(account, TransferType.CurrencyConversion, 1000m, Day(1), counter: Pln(3700m, Day(1)), rate: 3.80m),
            TransferIn(account, TransferType.CurrencyConversion, 1000m, Day(2), counter: Pln(3900m, Day(2)), rate: 3.95m),
            Buy(account, "AAPL.US", position: "A", quantity: 10m, price: 50m, amount: 500m, Day(3), rate: 3.90m),
            Sell(account, "AAPL.US", position: "A", quantity: 5m, price: 60m, amount: 300m, Day(5), rate: 3.90m),
            Dividend(account, "AAPL.US", 10m, Day(6), rate: 3.95m),
            Tax(account, "AAPL.US", 1.5m, Day(6), rate: 3.95m),
            Fee(account, 2m, Day(6), rate: 3.95m),
            TransferOut(account, TransferType.CurrencyConversion, 500m, Day(7), counter: Pln(2050m, Day(7)), rate: 4.05m),
        ], Prices(("AAPL.US", "USD", (Day(20), 60m))));

        var figures = PortfolioFigures.For([usd], Today, Day(20));

        Assert.Equal(7600m, figures.PutInPln);
        Assert.Equal(1900m, figures.TakenOutPln);
        Assert.Equal(5700m, figures.NetDepositsPln);
        Assert.Equal(190m, figures.RealizedGainsPln);
        Assert.Equal(190m, figures.RealizedResultPln);
        Assert.Equal(32.3m, figures.DividendsNetPln);
        Assert.Equal(32.3m, figures.PassiveIncomePln);
        Assert.Equal(7.6m, figures.FeesPln);
        Assert.Equal(200m, figures.UnrealizedPln);
        Assert.Equal(6426m, figures.ValuePln);
        Assert.Equal(726m, figures.TotalProfitPln);
        Assert.Equal(311.3m, figures.CurrencyDifferencePln);
        Assert.Equal(3.8m, usd.Item2.AverageRate);
        Assert.Equal(5700m, usd.Item2.PlnSpent);
        PlnIdentityHolds(figures);
        DepositsAddUp(figures);
    }

    [Fact]
    public void MoneyMovedBetweenOwnAccountsIsNeitherPutInNorTakenOut()
    {
        var plnAccount = AnAccount(number: "1001");
        var usdAccount = AnAccount("USD", number: "1002");
        var pln = Read(plnAccount, [
            Deposit(plnAccount, 10000m, Day(1)),
            TransferOut(plnAccount, TransferType.CurrencyConversion, 3700m, Day(2), counterparty: "1002",
                counter: Foreign(1000m, "USD", 3.7m, Day(2))),
            TransferOut(plnAccount, TransferType.Subaccount, 100m, Day(3), counterparty: "9999"),
        ]);
        var usd = Read(usdAccount, [
            TransferIn(usdAccount, TransferType.CurrencyConversion, 1000m, Day(2), counterparty: "1001",
                counter: Pln(3700m, Day(2)), rate: 3.80m),
        ]);

        var figures = PortfolioFigures.For([pln, usd], Today, Day(20));

        Assert.Equal(3800m, pln.Item2.TakenOut);
        Assert.Equal(10000m, figures.PutInPln);
        Assert.Equal(100m, figures.TakenOutPln);
        Assert.Equal(9900m, figures.NetDepositsPln);
        Assert.Equal(10200m, figures.ValuePln);
        Assert.Equal(300m, figures.TotalProfitPln);
        Assert.Equal(300m, figures.CurrencyDifferencePln);
        PlnIdentityHolds(figures);
        DepositsAddUp(figures);
    }

    [Fact]
    public void IkeLegsOfTheSameAmountWithinADayInTwoOwnAccountsAreInternal()
    {
        var plnAccount = AnAccount(number: "1001");
        var ikeAccount = AnAccount(number: "1003");
        var day = new DateTimeOffset(Day(2).ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
        var pln = Read(plnAccount, [
            Deposit(plnAccount, 2000m, Day(1)),
            TransferAt(plnAccount, OperationKind.TransferOut, TransferType.Ike, 500m, day),
        ]);
        var ike = Read(ikeAccount, [
            TransferAt(ikeAccount, OperationKind.TransferIn, TransferType.Ike, 500m, day.AddHours(3)),
            TransferAt(ikeAccount, OperationKind.TransferIn, TransferType.Ike, 300m, day.AddDays(3)),
        ]);

        var figures = PortfolioFigures.For([pln, ike], Today, Day(20));

        Assert.Equal(2300m, figures.PutInPln);
        Assert.Equal(0m, figures.TakenOutPln);
        Assert.Equal(2300m, figures.NetDepositsPln);
        Assert.Equal(0m, figures.TotalProfitPln);
    }

    [Fact]
    public void IkeLegsMoreThanADayApartOrInOneAccountStayPutInAndTakenOut()
    {
        var plnAccount = AnAccount(number: "1001");
        var ikeAccount = AnAccount(number: "1003");
        var day = new DateTimeOffset(Day(2).ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
        var pln = Read(plnAccount, [
            Deposit(plnAccount, 2000m, Day(1)),
            TransferAt(plnAccount, OperationKind.TransferOut, TransferType.Ike, 500m, day),
            TransferAt(plnAccount, OperationKind.TransferIn, TransferType.Ike, 200m, day.AddHours(1)),
            TransferAt(plnAccount, OperationKind.TransferOut, TransferType.Ike, 200m, day.AddHours(2)),
        ]);
        var ike = Read(ikeAccount, [
            TransferAt(ikeAccount, OperationKind.TransferIn, TransferType.Ike, 500m, day.AddDays(1).AddMinutes(1)),
        ]);

        var figures = PortfolioFigures.For([pln, ike], Today, Day(20));

        Assert.Equal(2700m, figures.PutInPln);
        Assert.Equal(700m, figures.TakenOutPln);
        Assert.Equal(2000m, figures.NetDepositsPln);
    }
}
