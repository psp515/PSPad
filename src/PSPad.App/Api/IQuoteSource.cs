using PSPad.Contracts;

namespace PSPad.App.Api;

public interface IQuoteSource
{
    Task<QuoteSeriesView?> QuotesAsync(string symbol, DateOnly from, bool refresh, CancellationToken ct = default);

    Task<IReadOnlyList<NbpRateView>?> RatesAsync(string currency, DateOnly from, DateOnly to, CancellationToken ct = default);
}
