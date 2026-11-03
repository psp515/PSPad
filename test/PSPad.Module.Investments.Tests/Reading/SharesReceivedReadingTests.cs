using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Reading.Market;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class SharesReceivedReadingTests
{
    static void IdentityHolds(AccountFigures figures) =>
        Assert.Equal(figures.RealizedGains + figures.RealizedLosses + figures.Unrealized + figures.PassiveIncome - figures.Fees,
            figures.TotalProfit);

    static void PlnIdentityHolds(PortfolioFigures figures) =>
        Assert.Equal(figures.RealizedGainsPln + figures.RealizedLossesPln + figures.UnrealizedPln + figures.PassiveIncomePln
            - figures.FeesPln + figures.CurrencyDifferencePln, figures.TotalProfitPln);

    [Fact]
    public void ReceivedSharesOpenALotAtTheirCostWithoutTouchingCash()
    {
        var account = AnAccount();
        var operations = new[] {
            Deposit(account, 100m, Day(1)),
            Received(account, "NEW.PL", position: "P", quantity: 7m, price: 0.002m, amount: 0.01m, Day(2)),
        };

        var (open, _, _) = Lots.Of(operations);
        var figures = AccountFigures.For(account, operations, Prices(("NEW.PL", "PLN", (Day(5), 3m))), PlnOnly, Day(5), NoMoves);

        var lot = Assert.Single(open);
        Assert.Equal(("P", "NEW.PL", 7m, 0.01m), (lot.Key, lot.Ticker, lot.Quantity, lot.Cost));
        Assert.Equal(100m, figures.Cash);
        Assert.Equal(21m, figures.PositionsValue);
        Assert.Equal(20.99m, figures.Unrealized);
        Assert.Equal(0.01m, figures.PassiveIncome);
        Assert.Equal(21m, figures.TotalProfit);
        IdentityHolds(figures);
    }

    [Fact]
    public void SellingReceivedSharesRealisesProceedsLessTheReceivedCost()
    {
        var account = AnAccount();
        var operations = new[] {
            Deposit(account, 100m, Day(1)),
            Received(account, "NEW.PL", position: "P", quantity: 7m, price: 0.002m, amount: 0.01m, Day(2)),
            Sell(account, "NEW.PL", position: "P", quantity: 7m, price: 25m, amount: 175m, Day(4)),
        };

        var figures = AccountFigures.For(account, operations, Prices(), PlnOnly, Day(5), NoMoves);

        Assert.Empty(figures.Positions);
        Assert.Equal(174.99m, figures.RealizedGains);
        Assert.Equal(275m, figures.Cash);
        Assert.Equal(175m, figures.TotalProfit);
        Assert.Equal(0.01m, figures.PassiveIncome);
        IdentityHolds(figures);
    }

    [Fact]
    public void TheIdentityHoldsInPlnForAForeignAccountWithReceivedShares()
    {
        var account = AnAccount("USD");
        var operations = new[] {
            Deposit(account, 100m, Day(1), rate: 3.80m),
            Received(account, "NEW.US", position: "P", quantity: 5m, price: 2m, amount: 10m, Day(2), rate: 3.85m),
            Sell(account, "NEW.US", position: "P", quantity: 2m, price: 6m, amount: 12m, Day(4), rate: 3.90m),
        };
        var fx = Rates(("USD", Day(1), 3.80m), ("USD", Day(20), 4.00m));
        var figures = AccountFigures.For(account, operations, Prices(("NEW.US", "USD", (Day(20), 7m))), fx, Day(20), NoMoves);
        var entries = new (InvestmentAccount, AccountFigures, IReadOnlyList<InvestmentOperation>)[] { (account, figures, operations) };

        var totals = PortfolioFigures.For(entries, fx, Day(20), NoMoves);

        IdentityHolds(figures);
        Assert.Equal(112m, figures.Cash);
        Assert.Equal(8m, figures.RealizedGains);
        PlnIdentityHolds(totals);
        PlnIdentityHolds(totals.Accounts[account.Id]);
        Assert.Equal(38.5m, totals.PassiveIncomePln);
    }

    [Fact]
    public void ValueOverTimeCountsReceivedSharesWithoutCash()
    {
        var account = AnAccount();
        var operations = new[] {
            Deposit(account, 100m, Day(1)),
            Received(account, "NEW.PL", position: "P", quantity: 10m, price: 1m, amount: 10m, Day(2)),
        };

        var points = ValueOverTime.For(account, operations, Prices(("NEW.PL", "PLN", (Day(2), 3m))), PlnOnly, Day(2), Day(2), Utc);

        var point = Assert.Single(points);
        Assert.Equal(130m, point.Value);
        Assert.Equal(100m, point.NetDeposits);
    }

    [Fact]
    public void ReceivedSharesShowAsTheirOwnMarker()
    {
        var account = AnAccount();
        var received = Received(account, "NEW.PL", position: "P", quantity: 10m, price: 1m, amount: 10m, Day(2));

        var marker = Assert.Single(PriceMarkers.For("NEW.PL", [received], Utc));

        Assert.Equal(new PriceMarker(Day(2), MarkerKind.Received, 1m, 10m, 10m, received.Id), marker);
    }
}
