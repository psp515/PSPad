using PSPad.Contracts;

namespace PSPad.App.State.Investments;

public interface IQuoteStore
{
    Task SaveQuoteAsync(QuoteSeriesView series, DateOnly from);

    Task<StoredQuote?> QuoteAsync(string symbol);

    Task SaveRatesAsync(string currency, IReadOnlyList<NbpRateView> rates, DateOnly from);

    Task<StoredRates> RatesAsync(string currency);

    Task ClearAsync();
}
