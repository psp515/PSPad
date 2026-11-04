using PSPad.App.Components.Investments;
using PSPad.App.Resources;
using PSPad.App.Tests.Localization;
using PSPad.App.Tests.State;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components.Investments;

[UnitTest]
public class InvestmentStatTilesTests
{
    static AccountFigures AnAccount(
        string currency, decimal? averageRate = null, decimal? plnSpent = null, decimal movedIn = 0m, decimal movedOut = 0m) =>
        new(Guid.NewGuid(), currency, PutIn: 1500m, TakenOut: 300m, MovedIn: movedIn, MovedOut: movedOut, Cash: 0m, PositionsValue: 0m, Value: 0m, TotalProfit: 0m,
            RealizedGains: 60m, RealizedLosses: -50m, Unrealized: 2m, DividendsNet: 90m, PassiveIncome: 98m, Fees: 5m,
            averageRate, plnSpent, []);

    static Dictionary<string, string> Texts(IEnumerable<StatTile> tiles) => tiles.ToDictionary(tile => tile.Label, tile => tile.Text);

    [Fact]
    public void AnAccountShowsWhatWentInAndOutTheNettedRealizedResultAndDividendsNet()
    {
        using var culture = new CultureScope("en-US");

        var tiles = Texts(InvestmentStatTiles.For(AnAccount("PLN"), TestLocalizers.For<InvestmentsStrings>()));

        Assert.Equal("1,500.00 PLN", tiles["Put in"]);
        Assert.Equal("300.00 PLN", tiles["Taken out"]);
        Assert.Equal("+10.00 PLN", tiles["Realized result"]);
        Assert.Equal("+60.00 PLN", tiles["Realized gains"]);
        Assert.Equal("+90.00 PLN", tiles["Dividends net"]);
        Assert.DoesNotContain("Average rate", tiles.Keys);
        Assert.DoesNotContain("Moved in", tiles.Keys);
        Assert.DoesNotContain("Moved out", tiles.Keys);
    }

    [Fact]
    public void MovesBetweenOwnAccountsShowOnlyWhenThereAreAny()
    {
        using var culture = new CultureScope("en-US");

        var tiles = Texts(InvestmentStatTiles.For(AnAccount("PLN", movedIn: 50m, movedOut: 4200m),
            TestLocalizers.For<InvestmentsStrings>()));

        Assert.Equal("50.00 PLN", tiles["Moved in"]);
        Assert.Equal("4,200.00 PLN", tiles["Moved out"]);
    }

    [Fact]
    public void ACurrencyAccountShowsTheAverageRateAndThePlnSpentOnIt()
    {
        using var culture = new CultureScope("en-US");

        var tiles = Texts(InvestmentStatTiles.For(AnAccount("USD", 3.65123m, 7300m), TestLocalizers.For<InvestmentsStrings>()));

        Assert.Equal("1 USD = 3.6512 PLN", tiles["Average rate"]);
        Assert.Equal("7,300.00 PLN", tiles["PLN spent on USD"]);
    }

    [Fact]
    public void AllAccountsShowTheCurrencyDifferenceInPln()
    {
        using var culture = new CultureScope("en-US");
        var portfolio = new PortfolioFigures(ValuePln: 0m, CashPln: 0m, PutInPln: 2000m, TakenOutPln: 0m, MovedInPln: 0m, MovedOutPln: 0m,
            TotalProfitPln: 0m,
            RealizedGainsPln: 0m, RealizedLossesPln: -20m, UnrealizedPln: 0m, DividendsNetPln: 0m, PassiveIncomePln: 0m,
            FeesPln: 0m, CurrencyDifferencePln: 12.5m, MissingRates: false);

        var tiles = Texts(InvestmentStatTiles.For(portfolio, TestLocalizers.For<InvestmentsStrings>()));

        Assert.Equal("2,000.00 PLN", tiles["Put in"]);
        Assert.Equal("−20.00 PLN", tiles["Realized result"]);
        Assert.Equal("+12.50 PLN", tiles["Currency difference"]);
    }

    [Fact]
    public void PolishLabelsExistForEveryTile()
    {
        using var culture = new CultureScope("pl-PL");

        var tiles = InvestmentStatTiles.For(AnAccount("USD", 3.65m, 7300m), TestLocalizers.For<InvestmentsStrings>());

        Assert.Contains(tiles, tile => tile.Label == "Wpłacono");
        Assert.Contains(tiles, tile => tile.Text == "1 USD = 3,6500 PLN");
    }
}
