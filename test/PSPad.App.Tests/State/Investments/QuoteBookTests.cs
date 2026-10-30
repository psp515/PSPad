using PSPad.App.State.Investments;
using PSPad.Contracts;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class QuoteBookTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly From = new(2026, 1, 5);

    readonly StubQuoteSource _source = new();
    readonly InMemoryQuoteCache _cache = new();

    QuoteBook Book() => new(_source, _cache, new FixedClock(Now));

    static QuoteSeriesView Series(string symbol, DateTimeOffset fetchedAt, decimal close = 10m, string first = "2026-01-02") =>
        new(symbol, "USD", [new QuoteCloseView(DateOnly.Parse(first), close)], null, fetchedAt, false);

    static (string, string)[] One(string symbol = "AAPL.US") => [("AAPL", symbol)];

    [Fact]
    public async Task ACopyFetchedTodayThatCoversTheStartIsNotRequestedAgain()
    {
        await _cache.SaveQuoteAsync(Series("AAPL.US", Now.AddHours(-2)), From);

        var prices = await Book().PricesAsync(One(), From, false, true, TestContext.Current.CancellationToken);

        Assert.Empty(_source.QuotesAsked);
        Assert.Equal(10m, prices["AAPL"].Closes[0].Close);
    }

    [Fact]
    public async Task ACopyFromYesterdayIsRequestedAgainAndStored()
    {
        await _cache.SaveQuoteAsync(Series("AAPL.US", Now.AddDays(-1)), From);
        _source.Series["AAPL.US"] = Series("AAPL.US", Now, 11m);

        var prices = await Book().PricesAsync(One(), From, false, true, TestContext.Current.CancellationToken);

        Assert.Single(_source.QuotesAsked);
        Assert.Equal(11m, prices["AAPL"].Closes[0].Close);
        Assert.Equal(11m, (await _cache.QuoteAsync("AAPL.US"))!.Series.Closes[0].Close);
    }

    [Fact]
    public async Task ACopyStoredForALaterStartIsRequestedAgain()
    {
        await _cache.SaveQuoteAsync(Series("AAPL.US", Now), From.AddDays(30));
        _source.Series["AAPL.US"] = Series("AAPL.US", Now, 12m);

        await Book().PricesAsync(One(), From, false, true, TestContext.Current.CancellationToken);

        Assert.Single(_source.QuotesAsked);
    }

    [Fact]
    public async Task ANarrowerRequestKeepsTheEarlierHistory()
    {
        await _cache.SaveQuoteAsync(Series("AAPL.US", Now.AddDays(-1), 10m, "2026-01-05"), From);
        _source.Series["AAPL.US"] = Series("AAPL.US", Now, 11m, "2026-02-10");

        await Book().PricesAsync(One(), From.AddDays(30), false, true, TestContext.Current.CancellationToken);
        var offline = await Book().PricesAsync(One(), From, false, false, TestContext.Current.CancellationToken);

        Assert.Equal(From, (await _cache.QuoteAsync("AAPL.US"))!.From);
        Assert.Equal([new DailyClose(From, 10m), new DailyClose(new DateOnly(2026, 2, 10), 11m)], offline["AAPL"].Closes);
    }

    [Fact]
    public async Task RefreshRequestsAFreshCopyAnyway()
    {
        await _cache.SaveQuoteAsync(Series("AAPL.US", Now), From);
        _source.Series["AAPL.US"] = Series("AAPL.US", Now, 13m);

        await Book().PricesAsync(One(), From, true, true, TestContext.Current.CancellationToken);

        Assert.Equal([("AAPL.US", true)], _source.QuotesAsked);
    }

    [Fact]
    public async Task OfflineUsesTheStoredCopyAndAsksNothing()
    {
        await _cache.SaveQuoteAsync(Series("AAPL.US", Now.AddDays(-9)), From);
        var book = Book();

        var prices = await book.PricesAsync(One(), From, true, false, TestContext.Current.CancellationToken);

        Assert.Empty(_source.QuotesAsked);
        Assert.Equal(10m, prices["AAPL"].Closes[0].Close);
        Assert.Equal(Now.AddDays(-9), book.FetchedAt("AAPL.US"));
    }

    [Fact]
    public async Task OfflineWithNothingStoredLeavesTheInstrumentOut()
    {
        var prices = await Book().PricesAsync(One(), From, false, false, TestContext.Current.CancellationToken);

        Assert.Empty(prices);
    }

    [Fact]
    public async Task AFailedRequestKeepsTheStoredCopyAndMarksItStale()
    {
        await _cache.SaveQuoteAsync(Series("AAPL.US", Now.AddDays(-3)), From);
        var book = Book();

        var prices = await book.PricesAsync(One(), From, false, true, TestContext.Current.CancellationToken);

        Assert.Equal(10m, prices["AAPL"].Closes[0].Close);
        Assert.True(book.IsStale("AAPL.US"));
        Assert.Equal(10m, (await _cache.QuoteAsync("AAPL.US"))!.Series.Closes[0].Close);
    }

    [Fact]
    public async Task AFailedRequestAndASavedSeriesEachMoveTheGeneration()
    {
        await _cache.SaveQuoteAsync(Series("AAPL.US", Now.AddDays(-1)), From);
        var book = Book();
        var before = book.Generation;

        await book.PricesAsync(One(), From, false, true, TestContext.Current.CancellationToken);
        var afterFailure = book.Generation;
        _source.Series["AAPL.US"] = Series("AAPL.US", Now);
        await book.PricesAsync(One(), From, true, true, TestContext.Current.CancellationToken);

        Assert.True(afterFailure > before);
        Assert.True(book.Generation > afterFailure);
    }

    [Fact]
    public void TheQuoteDayIsTheUtcDay() => Assert.Equal(new DateOnly(2026, 10, 30), Book().QuoteDay);

    [Fact]
    public async Task AFreshCopyIsNotStale()
    {
        _source.Series["AAPL.US"] = Series("AAPL.US", Now);
        var book = Book();

        await book.PricesAsync(One(), From, false, true, TestContext.Current.CancellationToken);

        Assert.False(book.IsStale("AAPL.US"));
        Assert.Equal(Now, book.FetchedAt("AAPL.US"));
    }

    [Fact]
    public async Task AtMostFourSymbolsAreRequestedAtOnce()
    {
        var instruments = Enumerable.Range(0, 10).Select(i => ($"T{i}", $"S{i}")).ToArray();
        foreach (var (_, symbol) in instruments)
        {
            _source.Series[symbol] = Series(symbol, Now);
        }

        var prices = await Book().PricesAsync(instruments, From, false, true, TestContext.Current.CancellationToken);

        Assert.Equal(10, prices.Count);
        Assert.Equal(4, _source.MostInFlight);
    }

    [Fact]
    public async Task TwoTickersOnOneSymbolAskOnce()
    {
        _source.Series["X"] = Series("X", Now);

        var prices = await Book().PricesAsync([("A", "X"), ("B", "X")], From, false, true, TestContext.Current.CancellationToken);

        Assert.Single(_source.QuotesAsked);
        Assert.Equal(2, prices.Count);
    }

    [Fact]
    public async Task NewRatesAreMergedByDateIntoTheStoredSeries()
    {
        await _cache.SaveRatesAsync("EUR", [new NbpRateView("EUR", 4.2m, new DateOnly(2026, 1, 5)), new NbpRateView("EUR", 4.3m, new DateOnly(2026, 1, 6))], From);
        _source.Rates["EUR"] = [new NbpRateView("EUR", 4.35m, new DateOnly(2026, 1, 6)), new NbpRateView("EUR", 4.4m, new DateOnly(2026, 1, 7))];

        var book = await Book().FxAsync(["EUR"], From, new DateOnly(2026, 1, 7), true, TestContext.Current.CancellationToken);

        Assert.Equal([("EUR", new DateOnly(2026, 1, 7), new DateOnly(2026, 1, 7))], _source.RatesAsked);
        Assert.Equal(4.4m, book.ToPln("EUR", new DateOnly(2026, 1, 7)));
        Assert.Equal(3, (await _cache.RatesAsync("EUR")).Rates.Count);
    }

    [Fact]
    public async Task OfflineRatesComeFromTheStoredSeries()
    {
        await _cache.SaveRatesAsync("EUR", [new NbpRateView("EUR", 4.2m, new DateOnly(2026, 1, 5))], From);

        var book = await Book().FxAsync(["EUR", "PLN"], From, new DateOnly(2026, 1, 9), false, TestContext.Current.CancellationToken);

        Assert.Empty(_source.RatesAsked);
        Assert.Equal(4.2m, book.ToPln("EUR", new DateOnly(2026, 1, 9)));
        Assert.Equal(1m, book.ToPln("PLN", new DateOnly(2026, 1, 9)));
    }

    [Fact]
    public async Task AFailedRatesRequestFallsBackToTheStoredSeries()
    {
        await _cache.SaveRatesAsync("USD", [new NbpRateView("USD", 3.9m, new DateOnly(2026, 1, 5))], From);

        var book = await Book().FxAsync(["USD"], From, new DateOnly(2026, 1, 9), true, TestContext.Current.CancellationToken);

        Assert.Equal(3.9m, book.ToPln("USD", new DateOnly(2026, 1, 9)));
    }

    [Fact]
    public async Task AnInstrumentListedAfterTheStartIsNotRequestedAgainOnASecondOpenTheSameDay()
    {
        _source.Series["NEW.US"] = Series("NEW.US", Now, first: "2026-06-01");
        var book = Book();

        await book.PricesAsync([("NEW", "NEW.US")], From, false, true, TestContext.Current.CancellationToken);
        await book.PricesAsync([("NEW", "NEW.US")], From, false, true, TestContext.Current.CancellationToken);

        Assert.Single(_source.QuotesAsked);
    }

    [Fact]
    public async Task ALatestPriceAfterTheLastCloseBecomesTheNewestPoint()
    {
        var latestAt = new DateTimeOffset(2026, 10, 30, 9, 30, 0, TimeSpan.Zero);
        _source.Series["AAPL.US"] = Series("AAPL.US", Now) with { Latest = new QuoteLatestView(latestAt, 14.5m) };

        var prices = await Book().PricesAsync(One(), From, false, true, TestContext.Current.CancellationToken);

        Assert.Equal(new DailyClose(new DateOnly(2026, 10, 30), 14.5m), prices["AAPL"].Closes[^1]);
        Assert.Equal(2, prices["AAPL"].Closes.Count);
    }

    [Fact]
    public async Task ALatestPriceOnTheDayOfTheLastCloseLeavesTheClosesUnchanged()
    {
        var latestAt = new DateTimeOffset(2026, 1, 2, 15, 0, 0, TimeSpan.Zero);
        _source.Series["AAPL.US"] = Series("AAPL.US", Now) with { Latest = new QuoteLatestView(latestAt, 14.5m) };

        var prices = await Book().PricesAsync(One(), From, false, true, TestContext.Current.CancellationToken);

        Assert.Equal([new DailyClose(new DateOnly(2026, 1, 2), 10m)], prices["AAPL"].Closes);
    }

    [Fact]
    public async Task ALatestPriceSurvivesOffline()
    {
        var latestAt = new DateTimeOffset(2026, 10, 30, 9, 30, 0, TimeSpan.Zero);
        await _cache.SaveQuoteAsync(Series("AAPL.US", Now) with { Latest = new QuoteLatestView(latestAt, 14.5m) }, From);

        var prices = await Book().PricesAsync(One(), From, false, false, TestContext.Current.CancellationToken);

        Assert.Equal(14.5m, prices["AAPL"].Closes[^1].Close);
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
