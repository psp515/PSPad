using PSPad.App.State.Investments;
using PSPad.Contracts;

namespace PSPad.App.Tests.State.Investments;

public sealed class InMemoryQuoteCache : IQuoteStore
{
    readonly Dictionary<string, StoredQuote> _quotes = [];
    readonly Dictionary<string, StoredRates> _rates = [];

    public Task SaveQuoteAsync(QuoteSeriesView series, DateOnly from)
    {
        _quotes[series.Symbol] = new StoredQuote(series, from);
        return Task.CompletedTask;
    }

    public Task<StoredQuote?> QuoteAsync(string symbol) =>
        Task.FromResult(_quotes.GetValueOrDefault(symbol));

    public Task SaveRatesAsync(string currency, IReadOnlyList<NbpRateView> rates, DateOnly from)
    {
        _rates[currency] = new StoredRates(rates, from);
        return Task.CompletedTask;
    }

    public Task<StoredRates> RatesAsync(string currency) =>
        Task.FromResult(_rates.GetValueOrDefault(currency) ?? new StoredRates([], null));

    public Task ClearAsync()
    {
        _quotes.Clear();
        _rates.Clear();
        return Task.CompletedTask;
    }
}
