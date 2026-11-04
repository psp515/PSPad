using PSPad.App.Components.Investments;
using PSPad.App.Resources;
using PSPad.App.State.Investments;
using PSPad.App.Tests.Localization;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Reading;
using PSPad.Module.Money.Preferences;
using PSPad.TestInfrastructure;
using static PSPad.App.Tests.State.Investments.Investing;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class SecondaryFiguresTests
{
    static readonly DateOnly Today = new(2026, 10, 30);
    static readonly DateOnly First = new(2026, 9, 1);

    readonly InMemoryReplica _replica = new();
    readonly StubInvestmentQuotes _quotes = new();
    readonly InvestmentAccount _pln = AnAccount("Main", "PLN");
    readonly InvestmentAccount _usd = AnAccount("Dollars", "USD");

    async Task<InvestmentOverview> OverviewAsync(string? defaultCurrency = null)
    {
        await _replica.SaveAsync(_pln);
        await _replica.SaveAsync(_usd);
        await _replica.SaveAsync(Deposit(_pln, 1_000m, First));
        await _replica.SaveAsync(Deposit(_usd, 500m, First.AddDays(3)));
        await _replica.SaveAsync(Buy(_usd, "XYZ.US", 2m, 100m, 200m, First.AddDays(4)));
        _quotes.PricesByTicker["XYZ.US"] = new PriceHistory("XYZ", "USD", [new DailyClose(Today.AddDays(-1), 150m)]);
        _quotes.Rates["USD"] = [new DailyRate(First, 4m)];
        if (defaultCurrency is not null)
        {
            var preferences = new MoneyPreferences();
            preferences.ApplyAll(MoneyPreferences.Decide(null, new SetDefaultCurrency(Guid.NewGuid(), Owner, defaultCurrency), Now));
            await _replica.SaveAsync(preferences);
        }

        return await new InvestmentQuery(_replica, _quotes)
            .LoadAsync(Owner, Today, TimeZoneInfo.Utc, true, false, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AUsdAccountShowsItsPlnFiguresBesideItsOwnWhenTheDefaultIsPln()
    {
        using var culture = new CultureScope("en-US");
        var overview = await OverviewAsync();

        var secondary = overview.SecondaryOf(_usd.Id);

        Assert.NotNull(secondary);
        Assert.Equal("≈ 2,400.00 PLN", secondary.Money(secondary.Pln.ValuePln));
        Assert.Equal("≈ +400.00 PLN", secondary.Signed(secondary.Pln.TotalProfitPln));
        Assert.Equal("≈ 1,200.00 PLN", secondary.MoneyFromAccount(300m));
        var tiles = InvestmentStatTiles.For(overview.Figures[_usd.Id], TestLocalizers.For<InvestmentsStrings>(), secondary);
        Assert.Equal("≈ 2,000.00 PLN", tiles.Single(tile => tile.Label == "Put in").Secondary);
    }

    [Fact]
    public async Task APlnAccountAndAllAccountsShowNoSecondLineWhenTheDefaultIsPln()
    {
        var overview = await OverviewAsync();

        Assert.Null(overview.SecondaryOf(_pln.Id));
        Assert.Null(overview.SecondaryOf(null));
        Assert.All(InvestmentStatTiles.For(overview.Portfolio, TestLocalizers.For<InvestmentsStrings>(), overview.SecondaryOf(null)),
            tile => Assert.Null(tile.Secondary));
    }

    [Fact]
    public async Task AEuroDefaultShowsEurosUnderEveryOtherCurrency()
    {
        using var culture = new CultureScope("en-US");
        _quotes.Rates["EUR"] = [new DailyRate(First, 4.5m)];

        var overview = await OverviewAsync("EUR");

        Assert.Contains("EUR", _quotes.CurrenciesAsked);
        var usd = overview.SecondaryOf(_usd.Id)!;
        Assert.Equal("≈ 533.33 EUR", usd.Money(usd.Pln.ValuePln));
        var all = overview.SecondaryOf(null)!;
        Assert.Equal("≈ 755.56 EUR", all.Money(all.Pln.ValuePln));
        Assert.NotNull(overview.SecondaryOf(_pln.Id));
    }

    [Fact]
    public async Task AMissingRateLeavesTheSecondLineOut()
    {
        var overview = await OverviewAsync("EUR");

        Assert.Null(overview.SecondaryOf(_usd.Id));
        Assert.Null(overview.SecondaryOf(null));
    }
}
