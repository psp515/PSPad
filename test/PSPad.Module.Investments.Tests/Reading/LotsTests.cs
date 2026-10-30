using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class LotsTests
{
    [Fact]
    public void BuysWithOnePositionIdFormOneLot()
    {
        var account = AnAccount();

        var (open, _, _) = Lots.Of([
            Buy(account, "ABC.US", position: "A", quantity: 2m, price: 50m, amount: 100m, Day(1)),
            Buy(account, "ABC.US", position: "A", quantity: 1m, price: 55m, amount: 55m, Day(2)),
        ]);

        var lot = Assert.Single(open);
        Assert.Equal("A", lot.Key);
        Assert.Equal(3m, lot.Quantity);
        Assert.Equal(155m, lot.Cost);
    }

    [Fact]
    public void APartialCloseTakesCostProRata()
    {
        var account = AnAccount();
        var sell = Sell(account, "ABC.US", position: "A", quantity: 1m, price: 40m, amount: 40m, Day(2));

        var (open, closed, oversold) = Lots.Of([
            Buy(account, "ABC.US", position: "A", quantity: 3m, price: 33.33m, amount: 100m, Day(1)),
            sell,
        ]);

        var lot = Assert.Single(open);
        Assert.Equal(2m, lot.Quantity);
        Assert.Equal(66.67m, lot.Cost);
        var result = Assert.Single(closed);
        Assert.Equal(sell.Id, result.SellId);
        Assert.Equal(40m, result.Proceeds);
        Assert.Equal(33.33m, result.Cost);
        Assert.Equal(6.67m, result.Result);
        Assert.Empty(oversold);
    }

    [Fact]
    public void AManualSellClosesTheOldestLotsFirst()
    {
        var account = AnAccount();

        var (open, closed, _) = Lots.Of([
            Buy(account, "ABC.US", position: null, quantity: 2m, price: 15m, amount: 30m, Day(2)),
            Buy(account, "ABC.US", position: null, quantity: 2m, price: 10m, amount: 20m, Day(1)),
            Sell(account, "ABC.US", position: null, quantity: 3m, price: 15m, amount: 45m, Day(3)),
        ]);

        var lot = Assert.Single(open);
        Assert.Equal(1m, lot.Quantity);
        Assert.Equal(15m, lot.Cost);
        Assert.Equal(10m, Assert.Single(closed).Result);
    }

    [Fact]
    public void SellingMoreThanTheLotHoldsClosesItAndCountsTheRestAsOversold()
    {
        var account = AnAccount();

        var (open, closed, oversold) = Lots.Of([
            Buy(account, "ABC.US", position: "A", quantity: 2m, price: 10m, amount: 20m, Day(1)),
            Sell(account, "ABC.US", position: "A", quantity: 3m, price: 12m, amount: 36m, Day(2)),
            Sell(account, "ABC.US", position: "Z", quantity: 1m, price: 12m, amount: 12m, Day(3)),
        ]);

        Assert.Empty(open);
        Assert.Equal([16m, 12m], closed.Select(result => result.Result));
        Assert.Equal(2m, oversold["ABC.US"]);
    }

    [Fact]
    public void DeletedOperationsAndOperationsAfterTheCutOffAreIgnored()
    {
        var account = AnAccount();
        var deleted = Delete(Buy(account, "ABC.US", position: "A", quantity: 2m, price: 10m, amount: 20m, Day(1)), account);
        var later = Buy(account, "ABC.US", position: "B", quantity: 1m, price: 10m, amount: 10m, Day(5));

        var (open, _, _) = Lots.Of([
            deleted,
            Buy(account, "ABC.US", position: "C", quantity: 4m, price: 10m, amount: 40m, Day(2)),
            later,
        ], until: later.At.AddSeconds(-1));

        Assert.Equal("C", Assert.Single(open).Key);
    }
}
