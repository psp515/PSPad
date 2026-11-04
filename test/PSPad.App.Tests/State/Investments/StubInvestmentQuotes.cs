using PSPad.App.State.Investments;
using PSPad.Module.Investments.Reading;

namespace PSPad.App.Tests.State.Investments;

public sealed class StubInvestmentQuotes : IInvestmentQuotes
{
    public Dictionary<string, PriceHistory> PricesByTicker { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, IReadOnlyList<DailyRate>> Rates { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<(string Ticker, string Symbol)> InstrumentsAsked { get; } = [];

    public DateOnly? PricesFrom { get; private set; }

    public List<string> CurrenciesAsked { get; } = [];

    public HashSet<string> Stale { get; } = new(StringComparer.Ordinal);

    public int Generation { get; set; }

    public DateOnly QuoteDay { get; set; } = new(2026, 10, 30);

    public Task<IReadOnlyDictionary<string, PriceHistory>> PricesAsync(
        IReadOnlyCollection<(string Ticker, string Symbol)> instruments, DateOnly from, bool refresh, bool online, CancellationToken ct)
    {
        InstrumentsAsked.AddRange(instruments);
        PricesFrom = from;
        IReadOnlyDictionary<string, PriceHistory> found = instruments
            .Where(instrument => PricesByTicker.ContainsKey(instrument.Ticker))
            .ToDictionary(instrument => instrument.Ticker, instrument => PricesByTicker[instrument.Ticker], StringComparer.Ordinal);
        return Task.FromResult(found);
    }

    public Task<FxBook> FxAsync(IReadOnlyCollection<string> currencies, DateOnly from, DateOnly to, bool online, CancellationToken ct)
    {
        CurrenciesAsked.AddRange(currencies);
        return Task.FromResult(new FxBook(Rates.Where(rate => currencies.Contains(rate.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(rate => rate.Key,
                rate => (IReadOnlyList<DailyRate>)[.. rate.Value.Where(daily => daily.Date >= from && daily.Date <= to)])));
    }

    public DateTimeOffset? FetchedAt(string symbol) => null;

    public bool IsStale(string symbol) => Stale.Contains(symbol);
}
