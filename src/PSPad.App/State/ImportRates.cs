using System.Globalization;
using Microsoft.Extensions.Localization;
using PSPad.App.Api;
using PSPad.App.Resources;
using PSPad.Module.Money.Import;

namespace PSPad.App.State;

public static class ImportRates
{
    const int AtOnce = 4;

    public static async Task<RateBook> FetchAsync(
        INbpRates nbp, bool online, DateOnly today, IReadOnlyCollection<RateRequest> requests, RateBook book,
        IStringLocalizer<BudgetsStrings> text)
    {
        var known = new Dictionary<RateRequest, ImportRate>(book.Known);
        var failed = new Dictionary<RateRequest, string>(book.Failed);

        if (!online)
        {
            foreach (var request in requests)
            {
                failed[request] = text["ImportRateOffline"];
            }

            return new RateBook(known, failed);
        }

        foreach (var request in requests.Where(request => request.Date > today))
        {
            failed[request] = text["ImportRateInFuture", request.Date.ToString("d", CultureInfo.CurrentCulture), request.Currency];
        }

        using var gate = new SemaphoreSlim(AtOnce);
        var answers = await Task.WhenAll(requests.Where(request => request.Date <= today).Select(async request =>
        {
            await gate.WaitAsync();
            try
            {
                return (request, view: await nbp.RateAsync(request.Currency, request.Date));
            }
            finally
            {
                gate.Release();
            }
        }));

        foreach (var (request, view) in answers)
        {
            failed.Remove(request);

            if (view is null)
            {
                failed[request] = text["ImportRateMissing", request.Currency, request.Date.ToString("d", CultureInfo.CurrentCulture)];
            }
            else
            {
                known[request] = new ImportRate(view.Rate, view.EffectiveDate);
            }
        }

        return new RateBook(known, failed);
    }
}
