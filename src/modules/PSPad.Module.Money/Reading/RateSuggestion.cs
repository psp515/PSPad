using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Reading;

public sealed record RateSuggestion(decimal RateToPln, DateOnly? RateDate)
{
    public static RateSuggestion? For(string currency, IEnumerable<MoneyEntry> entries) =>
        For(currency, entries, []);

    public static RateSuggestion? For(
        string currency, IEnumerable<MoneyEntry> entries, IEnumerable<BalanceSnapshot> snapshots) =>
        Latest(currency, entries
            .Where(entry => !entry.Deleted)
            .Select(entry => (entry.Money, entry.RecordedAt))
            .Concat(snapshots
                .Where(snapshot => !snapshot.Deleted)
                .SelectMany(snapshot => snapshot.Holdings.Select(holding => (holding.Money, snapshot.UpdatedAt)))));

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
