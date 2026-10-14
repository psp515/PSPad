using System.Globalization;
using PSPad.App.Api;
using PSPad.Module.Money.Import;

namespace PSPad.App.State;

public static class ImportRates
{
    public const string OfflineMessage = "Needs a rate: go online or fill the rate column.";

    const int AtOnce = 4;

    public static async Task<RateBook> FetchAsync(
        INbpRates nbp, bool online, DateOnly today, IReadOnlyCollection<RateRequest> requests, RateBook book)
    {
        var known = new Dictionary<RateRequest, ImportRate>(book.Known);
        var failed = new Dictionary<RateRequest, string>(book.Failed);

        if (!online)
        {
            foreach (var request in requests)
            {
                failed[request] = OfflineMessage;
            }

            return new RateBook(known, failed);
        }

        foreach (var request in requests.Where(request => request.Date > today))
        {
            failed[request] = string.Create(CultureInfo.InvariantCulture,
                $"{request.Date:yyyy-MM-dd} is in the future, so NBP has no {request.Currency} rate for it yet. Fill the rate column or change the date.");
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
                failed[request] = string.Create(CultureInfo.InvariantCulture,
                    $"No NBP rate for {request.Currency} on {request.Date:yyyy-MM-dd}.");
            }
            else
            {
                known[request] = new ImportRate(view.Rate, view.EffectiveDate);
            }
        }

        return new RateBook(known, failed);
    }
}
