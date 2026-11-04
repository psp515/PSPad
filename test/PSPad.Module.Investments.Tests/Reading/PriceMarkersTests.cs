using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Reading.Market;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class PriceMarkersTests
{
    [Fact]
    public void MarksTheBuysSellsAndDividendsOfOneTicker()
    {
        var account = AnAccount();
        var buy = Buy(account, "DEF.PL", position: "A", quantity: 10m, price: 50m, amount: 500m, Day(1));
        var sell = Sell(account, "DEF.PL", position: "A", quantity: 4m, price: 55m, amount: 220m, Day(3));
        var dividend = Dividend(account, "DEF.PL", 12m, Day(4));
        var operations = new[] {
            dividend, sell, buy,
            Buy(account, "JKL.PL", position: "B", quantity: 1m, price: 100m, amount: 100m, Day(2)),
            Tax(account, "DEF.PL", 2m, Day(4)),
            Delete(Buy(account, "DEF.PL", position: "C", quantity: 1m, price: 40m, amount: 40m, Day(2)), account),
        };

        var markers = PriceMarkers.For("DEF.PL", operations, Utc);

        Assert.Equal([
            new PriceMarker(Day(1), MarkerKind.Buy, 50m, 10m, 500m, buy.Id),
            new PriceMarker(Day(3), MarkerKind.Sell, 55m, 4m, 220m, sell.Id),
            new PriceMarker(Day(4), MarkerKind.Dividend, null, null, 12m, dividend.Id),
        ], markers);
    }

    [Fact]
    public void AMarkerSitsOnTheDayOfTheUsersZone()
    {
        var account = AnAccount();
        var buy = BuyAt(account, "DEF.PL", 10m, 50m, HalfPastMidnightInWarsaw);

        var marker = Assert.Single(PriceMarkers.For("DEF.PL", [buy], Warsaw));

        Assert.Equal(FirstOfNovember, marker.Day);
    }
}
