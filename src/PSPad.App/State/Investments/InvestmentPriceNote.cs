using PSPad.Module.Investments.Reading;

namespace PSPad.App.State.Investments;

public static class InvestmentPriceNote
{
    public static PriceNote For(
        AccountValueOnDay value, Func<string, string> symbolOf, Func<string, bool> isStale, Func<string, DateTimeOffset?> fetchedAt)
    {
        if (value.PriceDays.Count == 0)
        {
            return PriceNote.None;
        }

        if (value.AnyNoPrice)
        {
            return new PriceNote(PriceNoteKind.NoPrice);
        }

        var days = value.PriceDays.Values.Where(day => day is not null).Select(day => day!.Value).ToList();
        var symbols = value.PriceDays.Keys.Select(symbolOf).ToList();

        if (symbols.Any(isStale))
        {
            return days.Count == 0 ? new PriceNote(PriceNoteKind.NoPrice) : new PriceNote(PriceNoteKind.Stale, days.Min());
        }

        var fetched = symbols.Select(fetchedAt).Where(at => at is not null).Select(at => at!.Value).ToList();
        if (fetched.Count > 0)
        {
            return new PriceNote(PriceNoteKind.Fetched, At: fetched.Min());
        }

        return days.Count == 0 ? PriceNote.None : new PriceNote(PriceNoteKind.Day, days.Max());
    }
}
