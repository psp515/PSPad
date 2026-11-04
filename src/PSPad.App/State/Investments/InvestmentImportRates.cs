using PSPad.Module.Investments.Import;

namespace PSPad.App.State.Investments;

public static class InvestmentImportRates
{
    public const int LeadDays = 7;

    public static async Task<IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)>> FetchAsync(
        IInvestmentQuotes quotes, IReadOnlyCollection<ImportRateKey> needed, bool online, CancellationToken ct)
    {
        var found = new Dictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)>();
        if (needed.Count == 0)
        {
            return found;
        }

        var from = needed.Min(key => key.Day).AddDays(-LeadDays);
        var to = needed.Max(key => key.Day);
        var book = await quotes.FxAsync([.. needed.Select(key => key.Currency).Distinct()], from, to, online, ct);
        foreach (var key in needed)
        {
            if (book.RateOn(key.Currency, key.Day) is { } rate)
            {
                found[key] = (rate.Rate, rate.Date);
            }
        }

        return found;
    }
}
