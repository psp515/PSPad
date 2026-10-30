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

    static (AccountFigures, IReadOnlyList<InvestmentOperation>) Read(
        InvestmentAccount account, InvestmentOperation[] operations, IReadOnlyDictionary<string, PriceHistory> prices) =>
        (AccountFigures.For(account, operations, prices, Today, Day(20)), operations);

    static void PlnIdentityHolds(PortfolioFigures figures) =>
        Assert.Equal(figures.RealizedGainsPln + figures.RealizedLossesPln + figures.UnrealizedPln + figures.PassiveIncomePln
            - figures.FeesPln + figures.CurrencyDifferencePln, figures.TotalProfitPln);

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
}
