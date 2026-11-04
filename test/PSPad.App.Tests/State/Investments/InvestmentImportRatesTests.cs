using PSPad.App.State.Investments;
using PSPad.Contracts;
using PSPad.Module.Investments.Import;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentImportRatesTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    readonly StubQuoteSource _source = new();
    readonly InMemoryQuoteCache _cache = new();

    QuoteBook Book() => new(_source, _cache, new FixedClock(Now));

    static ImportRateKey Key(string currency, string day) => new(currency, DateOnly.Parse(day));

    static NbpRateView Rate(string effective, decimal rate) => new("USD", rate, DateOnly.Parse(effective));

    [Fact]
    public async Task EachCurrencyIsAskedOnceForTheFilesRangeStartingAWeekBeforeTheFirstDay()
    {
        _source.Rates["USD"] = [Rate("2026-03-02", 4.0m)];
        _source.Rates["EUR"] = [Rate("2026-03-02", 4.3m)];
        var needed = new[] { Key("USD", "2026-03-10"), Key("USD", "2026-03-04"), Key("USD", "2026-04-01"), Key("EUR", "2026-03-05") };

        await InvestmentImportRates.FetchAsync(Book(), needed, true, TestContext.Current.CancellationToken);

        Assert.Equal(2, _source.RatesAsked.Count);
        Assert.Contains(("USD", new DateOnly(2026, 2, 25), new DateOnly(2026, 4, 1)), _source.RatesAsked);
        Assert.Contains(("EUR", new DateOnly(2026, 2, 25), new DateOnly(2026, 4, 1)), _source.RatesAsked);
    }

    [Fact]
    public async Task ADayWithoutAFixingTakesTheLatestEarlierOneAndKeepsItsDate()
    {
        _source.Rates["USD"] = [Rate("2026-03-06", 4.1m)];

        var rates = await InvestmentImportRates.FetchAsync(Book(), [Key("USD", "2026-03-08")], true, TestContext.Current.CancellationToken);

        Assert.Equal((4.1m, new DateOnly(2026, 3, 6)), rates[Key("USD", "2026-03-08")]);
    }

    [Fact]
    public async Task ADayBeforeEveryStoredFixingGetsNoRate()
    {
        _source.Rates["USD"] = [Rate("2026-03-06", 4.1m)];

        var rates = await InvestmentImportRates.FetchAsync(Book(), [Key("USD", "2026-03-01")], true, TestContext.Current.CancellationToken);

        Assert.Empty(rates);
    }

    [Fact]
    public async Task OfflineUsesOnlyWhatIsStoredAndAsksNobody()
    {
        await _cache.SaveRatesAsync("USD", [Rate("2026-03-06", 4.1m)], new DateOnly(2026, 2, 1));

        var rates = await InvestmentImportRates.FetchAsync(Book(), [Key("USD", "2026-03-06")], false, TestContext.Current.CancellationToken);

        Assert.Empty(_source.RatesAsked);
        Assert.Equal(4.1m, rates[Key("USD", "2026-03-06")].Rate);
    }

    [Fact]
    public async Task NothingNeededAsksNobody()
    {
        var rates = await InvestmentImportRates.FetchAsync(Book(), [], true, TestContext.Current.CancellationToken);

        Assert.Empty(rates);
        Assert.Empty(_source.RatesAsked);
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
