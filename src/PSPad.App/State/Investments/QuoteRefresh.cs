using PSPad.Contracts;

namespace PSPad.App.State.Investments;

public static class QuoteRefresh
{
    public static bool Needed(StoredQuote? stored, DateOnly from, DateOnly today, bool refresh) =>
        refresh || stored is null || DateOnly.FromDateTime(stored.Series.FetchedAt.UtcDateTime) < today || stored.From > from;

    public static StoredQuote Widened(StoredQuote? stored, QuoteSeriesView fetched, DateOnly from)
    {
        if (stored is null || stored.From >= from || !string.Equals(stored.Series.Currency, fetched.Currency, StringComparison.Ordinal))
        {
            return new StoredQuote(fetched, from);
        }

        var fetchedDays = fetched.Closes.Select(close => close.Date).ToHashSet();
        IReadOnlyList<QuoteCloseView> closes =
        [
            .. stored.Series.Closes.Where(close => close.Date < from && !fetchedDays.Contains(close.Date))
                .Concat(fetched.Closes).OrderBy(close => close.Date)
        ];
        return new StoredQuote(fetched with { Closes = closes }, stored.From);
    }

    public static IReadOnlyList<NbpRateView> Merge(IReadOnlyList<NbpRateView> stored, IReadOnlyList<NbpRateView> fetched) =>
        stored.Concat(fetched)
            .GroupBy(rate => rate.EffectiveDate)
            .Select(group => group.Last())
            .OrderBy(rate => rate.EffectiveDate)
            .ToList();
}
