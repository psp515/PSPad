using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class ClosedPositionsTests
{
    [Fact]
    public void ATickerSoldOutIsClosedWithItsCostProceedsResultAndDates()
    {
        var account = AnAccount();
        var first = Buy(account, "DEF.PL", position: "A", quantity: 4m, price: 10m, amount: 40m, Day(1));
        var second = Buy(account, "DEF.PL", position: "B", quantity: 6m, price: 20m, amount: 120m, Day(2));
        var sell = Sell(account, "DEF.PL", position: null, quantity: 10m, price: 15m, amount: 150m, Day(5));

        var closed = Assert.Single(ClosedPositions.For([sell, second, first]));

        Assert.Equal("DEF.PL", closed.Ticker);
        Assert.Equal(10m, closed.Quantity);
        Assert.Equal(160m, closed.Cost);
        Assert.Equal(150m, closed.Proceeds);
        Assert.Equal(-10m, closed.Result);
        Assert.Equal(-0.0625m, closed.ResultShare);
        Assert.Equal(first.At, closed.FirstBought);
        Assert.Equal(sell.At, closed.LastSold);
        Assert.Equal(
            [
                new ClosedLot("A", first.At, 10m, sell.At, 15m, 4m, 40m, 60m),
                new ClosedLot("B", second.At, 20m, sell.At, 15m, 6m, 120m, 90m)
            ],
            closed.Lots);
    }

    [Fact]
    public void ATickerStillHeldIsNotClosed()
    {
        var account = AnAccount();
        var operations = new[] {
            Buy(account, "DEF.PL", position: "A", quantity: 4m, price: 10m, amount: 40m, Day(1)),
            Sell(account, "DEF.PL", position: "A", quantity: 1m, price: 12m, amount: 12m, Day(2)),
        };

        Assert.Empty(ClosedPositions.For(operations));
    }

    [Fact]
    public void ClosedTickersComeNewestSaleFirstAndDeletedOperationsDoNotCount()
    {
        var account = AnAccount();
        var operations = new[] {
            Buy(account, "OLD.PL", position: "A", quantity: 1m, price: 10m, amount: 10m, Day(1)),
            Sell(account, "OLD.PL", position: "A", quantity: 1m, price: 12m, amount: 12m, Day(3)),
            Buy(account, "NEW.PL", position: "B", quantity: 1m, price: 10m, amount: 10m, Day(1)),
            Sell(account, "NEW.PL", position: "B", quantity: 1m, price: 11m, amount: 11m, Day(4)),
            Delete(Buy(account, "GONE.PL", position: "C", quantity: 1m, price: 10m, amount: 10m, Day(1)), account),
            Delete(Sell(account, "GONE.PL", position: "C", quantity: 1m, price: 10m, amount: 10m, Day(5)), account),
        };

        Assert.Equal(["NEW.PL", "OLD.PL"], ClosedPositions.For(operations).Select(closed => closed.Ticker));
    }

    [Fact]
    public void ReceivedSharesCountAsCostAndAPartCloseSplitsTheProceeds()
    {
        var account = AnAccount();
        var received = Received(account, "NEW.PL", position: "P", quantity: 7m, price: 0.002m, amount: 0.01m, Day(1));
        var part = Sell(account, "NEW.PL", position: "P", quantity: 3m, price: 25m, amount: 75m, Day(2));
        var rest = Sell(account, "NEW.PL", position: "P", quantity: 4m, price: 26m, amount: 104m, Day(3));

        var closed = Assert.Single(ClosedPositions.For([received, part, rest]));

        Assert.Equal(0.01m, closed.Cost);
        Assert.Equal(179m, closed.Proceeds);
        Assert.Equal(178.99m, closed.Result);
        Assert.Equal(received.At, closed.FirstBought);
        Assert.Equal(rest.At, closed.LastSold);
        Assert.Equal([3m, 4m], closed.Lots.Select(lot => lot.Quantity));
        Assert.Equal(0.01m, closed.Lots.Sum(lot => lot.Cost));
        Assert.All(closed.Lots, lot => Assert.True(lot.Received));
        Assert.Null(closed.ResultShare);
    }

    [Fact]
    public void ALotsOpenPriceForgetsTheSharesAlreadySold()
    {
        var account = AnAccount();
        var operations = new[] {
            Buy(account, "DEF.PL", position: "A", quantity: 4m, price: 10m, amount: 40m, Day(1)),
            Sell(account, "DEF.PL", position: "A", quantity: 2m, price: 12m, amount: 24m, Day(2)),
            Buy(account, "DEF.PL", position: "A", quantity: 4m, price: 20m, amount: 80m, Day(3)),
            Sell(account, "DEF.PL", position: "A", quantity: 6m, price: 22m, amount: 132m, Day(4)),
        };

        var closed = Assert.Single(ClosedPositions.For(operations));

        Assert.Equal([10m, 16.66666667m], closed.Lots.Select(lot => lot.OpenPrice));
        Assert.Equal([20m, 100m], closed.Lots.Select(lot => lot.Cost));
        Assert.All(closed.Lots, lot => Assert.False(lot.Received));
    }

    [Fact]
    public void AnOversoldPartIsALotWithNoPurchase()
    {
        var account = AnAccount();
        var sell = Sell(account, "XYZ.PL", position: null, quantity: 2m, price: 5m, amount: 10m, Day(2));

        var closed = Assert.Single(ClosedPositions.For([sell]));

        Assert.Equal(new ClosedLot(null, null, null, sell.At, 5m, 2m, 0m, 10m), Assert.Single(closed.Lots));
        Assert.Null(closed.FirstBought);
        Assert.Null(closed.ResultShare);
    }
}
