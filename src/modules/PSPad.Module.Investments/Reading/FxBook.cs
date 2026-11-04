namespace PSPad.Module.Investments.Reading;

public sealed class FxBook
{
    const string Pln = "PLN";

    readonly Dictionary<string, IReadOnlyList<DailyRate>> rates;

    public FxBook(IReadOnlyDictionary<string, IReadOnlyList<DailyRate>> rates) =>
        this.rates = rates.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<DailyRate>)pair.Value.OrderBy(rate => rate.Date).ToList(),
            StringComparer.OrdinalIgnoreCase);

    public decimal? ToPln(string currency, DateOnly day)
    {
        if (string.Equals(currency, Pln, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        return rates.TryGetValue(currency, out var series)
            ? Dated.LastOnOrBefore(series, day, rate => rate.Date)?.Rate
            : null;
    }

    public DailyRate? RateOn(string currency, DateOnly day)
    {
        if (string.Equals(currency, Pln, StringComparison.OrdinalIgnoreCase))
        {
            return new DailyRate(day, 1m);
        }

        return rates.TryGetValue(currency, out var series) ? Dated.LastOnOrBefore(series, day, rate => rate.Date) : null;
    }

    public decimal? Convert(decimal amount, string from, string to, DateOnly day)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        return ToPln(from, day) is { } fromRate && ToPln(to, day) is { } toRate && toRate != 0
            ? amount * fromRate / toRate
            : null;
    }
}
