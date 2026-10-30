using PSPad.Module.Investments.Reading;

namespace PSPad.App.State.Investments;

public interface IInvestmentQuotes
{
    Task<IReadOnlyDictionary<string, PriceHistory>> PricesAsync(
        IReadOnlyCollection<(string Ticker, string Symbol)> instruments, DateOnly from, bool refresh, bool online, CancellationToken ct);

    Task<FxBook> FxAsync(IReadOnlyCollection<string> currencies, DateOnly from, DateOnly to, bool online, CancellationToken ct);

    DateTimeOffset? FetchedAt(string symbol);

    bool IsStale(string symbol);

    int Generation { get; }

    DateOnly QuoteDay { get; }
}
