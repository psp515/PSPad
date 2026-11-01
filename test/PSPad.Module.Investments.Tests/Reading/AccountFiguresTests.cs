using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Reading.Market;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class AccountFiguresTests
{
    static void IdentityHolds(AccountFigures figures) =>
        Assert.Equal(figures.RealizedGains + figures.RealizedLosses + figures.Unrealized + figures.PassiveIncome - figures.Fees,
            figures.TotalProfit);

    [Fact]
    public void TheIdentityHoldsForAMixedAccount()
    {
        var account = AnAccount("PLN");
        var operations = new[] {
            Deposit(account, 5000m, Day(1)),
            Buy(account, "DEF.PL", position: "A", quantity: 20m, price: 1.5m, amount: 30m, Day(2)),
            Buy(account, "JKL.PL", position: "B", quantity: 3m, price: 120m, amount: 360m, Day(3)),
            Sell(account, "JKL.PL", position: "B", quantity: 3m, price: 140m, amount: 420m, Day(10)),
            Buy(account, "MNO.PL", position: "C", quantity: 1m, price: 400m, amount: 400m, Day(4)),
            Sell(account, "MNO.PL", position: "C", quantity: 1m, price: 350m, amount: 350m, Day(11)),
            Dividend(account, "ABC.US", 12m, Day(12)), Tax(account, "ABC.US", 3m, Day(12)), Interest(account, 1m, Day(13)),
        };
        var prices = Prices(("DEF.PL", "PLN", (Day(20), 1.6m)));

        var figures = AccountFigures.For(account, operations, prices, PlnOnly, Day(20));

        Assert.Equal(60m, figures.RealizedGains);
        Assert.Equal(-50m, figures.RealizedLosses);
        Assert.Equal(2m, figures.Unrealized);
        Assert.Equal(10m, figures.PassiveIncome);
        Assert.Equal(figures.RealizedGains + figures.RealizedLosses + figures.Unrealized + figures.PassiveIncome - figures.Fees,
            figures.TotalProfit);
    }

    [Fact]
    public void CashDepositsAndFeesAddUpAndTheIdentityStillHolds()
    {
        var account = AnAccount();
        var operations = new[] {
            Deposit(account, 1000m, Day(1)),
            Withdrawal(account, 100m, Day(2)),
            Buy(account, "DEF.PL", position: "A", quantity: 10m, price: 20m, amount: 200m, Day(3)),
            Fee(account, 5m, Day(4)),
            Delete(Deposit(account, 700m, Day(5)), account),
        };

        var figures = AccountFigures.For(account, operations, Prices(("DEF.PL", "PLN", (Day(6), 21.5m))), PlnOnly, Day(6));

        Assert.Equal(900m, figures.NetDeposits);
        Assert.Equal(695m, figures.Cash);
        Assert.Equal(215m, figures.PositionsValue);
        Assert.Equal(910m, figures.Value);
        Assert.Equal(10m, figures.TotalProfit);
        Assert.Equal(5m, figures.Fees);
        IdentityHolds(figures);
    }

    [Fact]
    public void APositionTakesTheLastCloseOnOrBeforeTheDay()
    {
        var account = AnAccount();
        var operations = new[] { Buy(account, "DEF.PL", position: "A", quantity: 20m, price: 1.5m, amount: 30m, Day(2)) };

        var figures = AccountFigures.For(account, operations,
            History("DEF.PL", "PLN", (Day(25), 1.7m), (Day(18), 1.6m)), PlnOnly, Day(20));

        var position = Assert.Single(figures.Positions);
        Assert.Equal(account.Id, position.AccountId);
        Assert.Equal(20m, position.Quantity);
        Assert.Equal(1.5m, position.AveragePrice);
        Assert.Equal(1.6m, position.LastClose);
        Assert.Equal(Day(18), position.PriceDay);
        Assert.Equal(32m, position.Value);
        Assert.Equal(2m, position.Unrealized);
        Assert.Equal(PositionWarning.None, position.Warning);
    }

    [Fact]
    public void AnInstrumentWithoutPricesIsValuedAtCost()
    {
        var account = AnAccount();
        var operations = new[] {
            Deposit(account, 500m, Day(1)),
            Buy(account, "ABC.US", position: "A", quantity: 2m, price: 50m, amount: 100m, Day(2)),
        };

        var figures = AccountFigures.For(account, operations, Prices(), PlnOnly, Day(20));

        var position = Assert.Single(figures.Positions);
        Assert.Equal(100m, position.Value);
        Assert.Equal(0m, position.Unrealized);
        Assert.Null(position.LastClose);
        Assert.Equal(PositionWarning.NoPrice, position.Warning);
        Assert.Equal(500m, figures.Value);
    }

    [Fact]
    public void AnOversoldTickerCarriesAWarning()
    {
        var account = AnAccount();
        var operations = new[] {
            Buy(account, "DEF.PL", position: "A", quantity: 2m, price: 10m, amount: 20m, Day(1)),
            Sell(account, "DEF.PL", position: "A", quantity: 3m, price: 12m, amount: 36m, Day(2)),
        };

        var figures = AccountFigures.For(account, operations, Prices(("DEF.PL", "PLN", (Day(3), 11m))), PlnOnly, Day(3));

        var position = Assert.Single(figures.Positions);
        Assert.Equal(PositionWarning.Oversold, position.Warning);
        Assert.Equal(1m, position.OversoldBy);
        Assert.Equal(0m, position.Quantity);
        IdentityHolds(figures);
    }

    [Fact]
    public void AForeignPriceIsConvertedThroughPlnOnThePriceDay()
    {
        var account = AnAccount("EUR");
        var operations = new[] {
            Deposit(account, 2000m, Day(1)),
            Buy(account, "AAPL.US", position: "A", quantity: 2m, price: 150m, amount: 280m, Day(2)),
        };
        var fx = Rates(("USD", Day(19), 4.10m), ("USD", Day(20), 4.00m), ("EUR", Day(19), 4.25m), ("EUR", Day(20), 4.40m));

        var figures = AccountFigures.For(account, operations, Prices(("AAPL.US", "USD", (Day(19), 165m))), fx, Day(22));

        Assert.Equal(318.35m, Assert.Single(figures.Positions).Value);
        Assert.Equal(38.35m, figures.Unrealized);
        IdentityHolds(figures);
    }

    [Fact]
    public void AUsdStockInAPlnAccountIsConvertedAtTheNbpRate()
    {
        var account = AnAccount();
        var operations = new[] {
            Deposit(account, 2000m, Day(1)),
            Buy(account, "AAPL.US", position: "A", quantity: 2m, price: 150m, amount: 1200m, Day(2)),
        };

        var figures = AccountFigures.For(account, operations, Prices(("AAPL.US", "USD", (Day(20), 160m))),
            Rates(("USD", Day(19), 4.10m), ("USD", Day(20), 4.00m)), Day(20));

        Assert.Equal(1280m, figures.PositionsValue);
        Assert.Equal(80m, figures.Unrealized);
        Assert.Equal(2080m, figures.Value);
        IdentityHolds(figures);
    }

    [Fact]
    public void PencePricesAreDividedByAHundred()
    {
        var account = AnAccount();
        var operations = new[] { Buy(account, "TEL.UK", position: "A", quantity: 100m, price: 3.5m, amount: 350m, Day(2)) };

        var figures = AccountFigures.For(account, operations, Prices(("TEL.UK", "GBp", (Day(20), 75m))),
            Rates(("GBP", Day(20), 5m)), Day(20));

        Assert.Equal(375m, Assert.Single(figures.Positions).Value);
        Assert.Equal(25m, figures.Unrealized);
    }

    [Fact]
    public void AMissingRateValuesThePositionAtCost()
    {
        var account = AnAccount();
        var operations = new[] { Buy(account, "AAPL.US", position: "A", quantity: 2m, price: 150m, amount: 1200m, Day(2)) };

        var figures = AccountFigures.For(account, operations, Prices(("AAPL.US", "USD", (Day(20), 160m))), PlnOnly, Day(20));

        var position = Assert.Single(figures.Positions);
        Assert.Equal(1200m, position.Value);
        Assert.Equal(PositionWarning.NoPrice, position.Warning);
    }

    [Fact]
    public void TransfersArePutInAndTakenOutButNeverProfit()
    {
        var account = AnAccount();
        var operations = new[] {
            Deposit(account, 1000m, Day(1)),
            TransferIn(account, TransferType.Subaccount, 500m, Day(2), counterparty: "70001"),
            Withdrawal(account, 100m, Day(3)),
            TransferOut(account, TransferType.Ike, 200m, Day(4)),
            Buy(account, "DEF.PL", position: "A", quantity: 10m, price: 20m, amount: 200m, Day(5)),
        };

        var figures = AccountFigures.For(account, operations, Prices(("DEF.PL", "PLN", (Day(6), 23m))), PlnOnly, Day(6));

        Assert.Equal(1500m, figures.PutIn);
        Assert.Equal(300m, figures.TakenOut);
        Assert.Equal(1200m, figures.NetDeposits);
        Assert.Equal(1230m, figures.Value);
        Assert.Equal(30m, figures.TotalProfit);
        IdentityHolds(figures);
    }

    [Fact]
    public void DividendsNetTakeOffDividendTaxAndAddRefundsWhileInterestTaxStaysInPassiveIncome()
    {
        var account = AnAccount();
        var operations = new[] {
            Deposit(account, 1000m, Day(1)),
            Dividend(account, "ABC.US", 100m, Day(2)),
            Tax(account, "ABC.US", 15m, Day(2)),
            TaxRefund(account, "ABC.US", 5m, Day(3)),
            Interest(account, 10m, Day(4)),
            TaxNamingNoTicker(account, 2m, Day(4)),
        };

        var figures = AccountFigures.For(account, operations, Prices(), PlnOnly, Day(6));

        Assert.Equal(90m, figures.DividendsNet);
        Assert.Equal(98m, figures.PassiveIncome);
        Assert.Equal(98m, figures.TotalProfit);
        IdentityHolds(figures);
    }

    [Fact]
    public void TheRealizedResultNetsGainsAndLosses()
    {
        var account = AnAccount();
        var operations = new[] {
            Buy(account, "JKL.PL", position: "B", quantity: 3m, price: 120m, amount: 360m, Day(3)),
            Sell(account, "JKL.PL", position: "B", quantity: 3m, price: 140m, amount: 420m, Day(10)),
            Buy(account, "MNO.PL", position: "C", quantity: 1m, price: 400m, amount: 400m, Day(4)),
            Sell(account, "MNO.PL", position: "C", quantity: 1m, price: 350m, amount: 350m, Day(11)),
        };

        var figures = AccountFigures.For(account, operations, Prices(), PlnOnly, Day(12));

        Assert.Equal(60m, figures.RealizedGains);
        Assert.Equal(-50m, figures.RealizedLosses);
        Assert.Equal(10m, figures.RealizedResult);
        IdentityHolds(figures);
    }

    [Fact]
    public void ACurrencyAccountShowsTheAverageRatePaidAndThePlnStillSpentOnIt()
    {
        var account = AnAccount("USD");
        var operations = new[] {
            TransferIn(account, TransferType.CurrencyConversion, 100m, Day(1), counter: Pln(400m, Day(1)), rate: 3.9m),
            TransferIn(account, TransferType.CurrencyConversion, 100m, Day(2), counter: Pln(380m, Day(2)), rate: 3.9m),
            TransferOut(account, TransferType.CurrencyConversion, 50m, Day(3), counter: Pln(200m, Day(3)), rate: 3.9m),
            Dividend(account, "ABC.US", 10m, Day(4), rate: 3.9m),
            TaxRefund(account, "ABC.US", 1m, Day(5), rate: 3.9m),
        };

        var figures = AccountFigures.For(account, operations, Prices(), PlnOnly, Day(6));

        Assert.Equal(3.9m, figures.AverageRate);
        Assert.Equal(585m, figures.PlnSpent);
        Assert.Equal(200m, figures.PutIn);
        Assert.Equal(50m, figures.TakenOut);
        Assert.Equal(11m, figures.DividendsNet);
        IdentityHolds(figures);
    }

    [Fact]
    public void APlnAccountOrOneNeverFundedByAConversionHasNoAverageRate()
    {
        var pln = AnAccount();
        var usd = AnAccount("USD");

        var plnFigures = AccountFigures.For(pln, [Deposit(pln, 100m, Day(1))], Prices(), PlnOnly, Day(2));
        var usdFigures = AccountFigures.For(usd, [Deposit(usd, 100m, Day(1), rate: 3.9m)], Prices(), PlnOnly, Day(2));

        Assert.Null(plnFigures.AverageRate);
        Assert.Null(plnFigures.PlnSpent);
        Assert.Null(usdFigures.AverageRate);
        Assert.Null(usdFigures.PlnSpent);
    }
}
