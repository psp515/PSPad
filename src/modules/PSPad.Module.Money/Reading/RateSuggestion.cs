using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Reading;

public sealed record RateSuggestion(decimal RateToPln, DateOnly? RateDate)
{
    public static RateSuggestion? For(string currency, IEnumerable<MoneyEntry> entries) =>
        Latest(currency, entries.Where(entry => !entry.Deleted).Select(entry => (entry.Money, entry.RecordedAt)));

    static RateSuggestion? Latest(string currency, IEnumerable<(Money Money, DateTimeOffset WrittenAt)> amounts)
    {
        var code = currency.Trim().ToUpperInvariant();

        if (code == Currencies.Pln)
        {
            return new RateSuggestion(1m, null);
        }

        var latest = amounts
            .Where(amount => amount.Money.Currency == code)
            .OrderByDescending(amount => amount.Money.RateDate)
            .ThenByDescending(amount => amount.WrittenAt)
            .Select(amount => amount.Money)
            .FirstOrDefault();

        return latest is null ? null : new RateSuggestion(latest.RateToPln, latest.RateDate);
    }
}
