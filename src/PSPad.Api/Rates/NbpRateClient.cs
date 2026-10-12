using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PSPad.Abstractions;
using PSPad.Contracts;
using PSPad.Module.Money.Values;

namespace PSPad.Api.Rates;

public sealed class NbpRateClient(HttpClient http, IMemoryCache cache, IClock clock)
{
    const int LookbackDays = 7;
    static readonly DateOnly FirstPublishedDate = new(2002, 1, 2);

    public async Task<NbpRateView?> RateAsync(string currency, DateOnly date, CancellationToken ct)
    {
        var code = currency.Trim().ToUpperInvariant();

        if (code == Currencies.Pln)
        {
            return new NbpRateView(Currencies.Pln, 1m, date);
        }

        if (!Currencies.All.Contains(code))
        {
            return null;
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        if (date < FirstPublishedDate || date > today.AddDays(1))
        {
            return null;
        }

        var key = (nameof(NbpRateClient), code, date);

        if (cache.TryGetValue(key, out NbpRateView? cached))
        {
            return cached;
        }

        var rate = await FetchAsync(code, date, ct);

        if (rate is not null)
        {
            cache.Set(key, rate, NbpCacheLifetime.For(date, today));
        }

        return rate;
    }

    async Task<NbpRateView?> FetchAsync(string code, DateOnly date, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(PathFor(code, date), ct);

            if ((int)response.StatusCode is >= 400 and < 500)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new NbpUnavailableException($"NBP answered {(int)response.StatusCode}.");
            }

            var series = await response.Content.ReadFromJsonAsync<NbpSeries>(ct);

            if (series?.Rates is null)
            {
                throw new NbpUnavailableException("NBP answered without rates.");
            }

            var latest = series.Rates.Where(rate => rate.EffectiveDate <= date).MaxBy(rate => rate.EffectiveDate);
            return latest is null ? null : new NbpRateView(code, latest.Mid, latest.EffectiveDate);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException
            || (exception is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new NbpUnavailableException("NBP could not be reached.", exception);
        }
    }

    static string PathFor(string code, DateOnly date) =>
        string.Create(CultureInfo.InvariantCulture,
            $"api/exchangerates/rates/A/{code}/{date.AddDays(-LookbackDays):yyyy-MM-dd}/{date:yyyy-MM-dd}/?format=json");
}
