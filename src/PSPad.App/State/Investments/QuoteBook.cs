using PSPad.App.Api;
using PSPad.Contracts;
using PSPad.Module.Investments.Reading;

namespace PSPad.App.State.Investments;

public sealed class QuoteBook(IQuoteSource source, IQuoteStore store, TimeProvider clock) : IInvestmentQuotes
{
    const int AtOnce = 4;

    readonly Dictionary<string, (DateTimeOffset? FetchedAt, bool Stale)> _state = new(StringComparer.Ordinal);
    readonly Dictionary<string, DateOnly> _ratesAskedUpTo = new(StringComparer.OrdinalIgnoreCase);

    public int Generation { get; private set; }

    public DateOnly QuoteDay => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public DateTimeOffset? FetchedAt(string symbol) => _state.TryGetValue(symbol, out var state) ? state.FetchedAt : null;

    public bool IsStale(string symbol) => _state.TryGetValue(symbol, out var state) && state.Stale;

    public async Task<IReadOnlyDictionary<string, PriceHistory>> PricesAsync(
        IReadOnlyCollection<(string Ticker, string Symbol)> instruments, DateOnly from, bool refresh, bool online, CancellationToken ct)
    {
        var today = QuoteDay;
        using var gate = new SemaphoreSlim(AtOnce);

        var symbols = instruments.Select(instrument => instrument.Symbol).Distinct(StringComparer.Ordinal).ToArray();
        var series = await Task.WhenAll(symbols.Select(async symbol =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return (symbol, view: await SeriesAsync(symbol, from, refresh, online, today, ct));
            }
            finally
            {
                gate.Release();
            }
        }));

        var bySymbol = series.Where(entry => entry.view is not null).ToDictionary(entry => entry.symbol, entry => entry.view!, StringComparer.Ordinal);
        return instruments
            .Where(instrument => bySymbol.ContainsKey(instrument.Symbol))
            .GroupBy(instrument => instrument.Ticker, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var view = bySymbol[group.First().Symbol];
                    return new PriceHistory(view.Symbol, view.Currency, WithLatest(view));
                },
                StringComparer.Ordinal);
    }

    public async Task<FxBook> FxAsync(IReadOnlyCollection<string> currencies, DateOnly from, DateOnly to, bool online, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(AtOnce);
        var codes = currencies
            .Select(currency => currency.Trim().ToUpperInvariant())
            .Where(code => code.Length > 0 && code != "PLN")
            .Distinct()
            .ToArray();

        var books = await Task.WhenAll(codes.Select(async code =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return (code, rates: await RatesAsync(code, from, to, online, ct));
            }
            finally
            {
                gate.Release();
            }
        }));

        return new FxBook(books
            .Where(entry => entry.rates.Count > 0)
            .ToDictionary(
                entry => entry.code,
                entry => (IReadOnlyList<DailyRate>)entry.rates.Select(rate => new DailyRate(rate.EffectiveDate, rate.Rate)).ToList()));
    }

    async Task<QuoteSeriesView?> SeriesAsync(string symbol, DateOnly from, bool refresh, bool online, DateOnly today, CancellationToken ct)
    {
        var stored = await store.QuoteAsync(symbol);
        if (!online || !QuoteRefresh.Needed(stored, from, today, refresh))
        {
            Remember(symbol, stored?.Series, stored?.Series.Stale ?? false);
            return stored?.Series;
        }

        var fetched = await source.QuotesAsync(symbol, from, refresh, ct);
        if (fetched is null)
        {
            Remember(symbol, stored?.Series, true);
            Generation++;
            return stored?.Series;
        }

        var widened = QuoteRefresh.Widened(stored, fetched, from);
        await store.SaveQuoteAsync(widened.Series, widened.From);
        Generation++;
        Remember(symbol, widened.Series, widened.Series.Stale);
        return widened.Series;
    }

    async Task<IReadOnlyList<NbpRateView>> RatesAsync(string code, DateOnly from, DateOnly to, bool online, CancellationToken ct)
    {
        var (stored, storedFrom) = await store.RatesAsync(code);
        var coversStart = storedFrom <= from;
        var alreadyAsked = _ratesAskedUpTo.TryGetValue(code, out var upTo) && upTo >= to && coversStart;
        if (!online || alreadyAsked)
        {
            return stored;
        }

        var start = coversStart ? stored.Max(rate => rate.EffectiveDate).AddDays(1) : from;
        if (start > to)
        {
            _ratesAskedUpTo[code] = to;
            return stored;
        }

        var fetched = await source.RatesAsync(code, start, to, ct);
        if (fetched is null)
        {
            return stored;
        }

        _ratesAskedUpTo[code] = to;
        var merged = QuoteRefresh.Merge(stored, fetched);
        if (fetched.Count > 0)
        {
            await store.SaveRatesAsync(code, merged, coversStart ? storedFrom!.Value : from);
            Generation++;
        }

        return merged;
    }

    static List<DailyClose> WithLatest(QuoteSeriesView view)
    {
        var closes = view.Closes.Select(close => new DailyClose(close.Date, close.Close)).OrderBy(close => close.Date).ToList();
        if (view.Latest is { } latest && DateOnly.FromDateTime(latest.At.UtcDateTime) is var day && (closes.Count == 0 || day > closes[^1].Date))
        {
            closes.Add(new DailyClose(day, latest.Price));
        }

        return closes;
    }

    void Remember(string symbol, QuoteSeriesView? view, bool stale)
    {
        if (view is not null)
        {
            _state[symbol] = (view.FetchedAt, stale);
        }
    }
}
