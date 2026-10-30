using PSPad.App.Api;
using PSPad.Contracts;

namespace PSPad.App.Tests.State.Investments;

public sealed class StubQuoteSource : IQuoteSource
{
    public Dictionary<string, QuoteSeriesView?> Series { get; } = [];

    public Dictionary<string, IReadOnlyList<NbpRateView>?> Rates { get; } = [];

    public List<(string Symbol, bool Refresh)> QuotesAsked { get; } = [];

    public List<(string Currency, DateOnly From, DateOnly To)> RatesAsked { get; } = [];

    public int InFlight { get; private set; }

    public int MostInFlight { get; private set; }

    public async Task<QuoteSeriesView?> QuotesAsync(string symbol, DateOnly from, bool refresh, CancellationToken ct = default)
    {
        QuotesAsked.Add((symbol, refresh));
        MostInFlight = Math.Max(MostInFlight, ++InFlight);
        await Task.Delay(10);
        InFlight--;
        return Series.GetValueOrDefault(symbol);
    }

    public Task<IReadOnlyList<NbpRateView>?> RatesAsync(string currency, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        RatesAsked.Add((currency, from, to));
        return Task.FromResult(Rates.GetValueOrDefault(currency));
    }
}
