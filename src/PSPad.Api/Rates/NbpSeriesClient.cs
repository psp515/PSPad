using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PSPad.Abstractions;
using PSPad.Contracts;
using PSPad.Module.Money.Values;

namespace PSPad.Api.Rates;

public sealed class NbpSeriesClient(HttpClient http, IMemoryCache cache, IClock clock)
{
    const int ChunkDays = 93;
    const int MaxRangeDays = 366 * 25;
    static readonly DateOnly FirstPublishedDate = new(2002, 1, 2);

    public async Task<IReadOnlyList<NbpRateView>?> RatesAsync(string currency, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var code = currency.Trim().ToUpperInvariant();

        if (code == Currencies.Pln)
        {
            return [];
        }

        if (!Currencies.All.Contains(code) || from > to || from < FirstPublishedDate
            || to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var tomorrow = today.AddDays(1);
        if (to > tomorrow)
        {
            to = tomorrow;
        }

        var rates = new List<NbpRateView>();

        for (var start = from; start <= to; start = start.AddDays(ChunkDays))
        {
            var end = start.AddDays(ChunkDays - 1) < to ? start.AddDays(ChunkDays - 1) : to;
            rates.AddRange(await ChunkAsync(code, start, end, today, ct));
        }

        return rates.DistinctBy(rate => rate.EffectiveDate).OrderBy(rate => rate.EffectiveDate).ToList();
    }

    async Task<IReadOnlyList<NbpRateView>> ChunkAsync(string code, DateOnly start, DateOnly end, DateOnly today, CancellationToken ct)
    {
        var key = (nameof(NbpSeriesClient), code, start, end);

        if (cache.TryGetValue(key, out IReadOnlyList<NbpRateView>? cached) && cached is not null)
        {
            return cached;
        }

        var rates = await FetchAsync(code, start, end, ct);
        cache.Set(key, rates, NbpCacheLifetime.For(end, today));
        return rates;
    }

    async Task<IReadOnlyList<NbpRateView>> FetchAsync(string code, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var path = string.Create(CultureInfo.InvariantCulture,
            $"api/exchangerates/rates/A/{code}/{start:yyyy-MM-dd}/{end:yyyy-MM-dd}/?format=json");

        try
        {
            using var response = await http.GetAsync(path, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return [];
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

            return series.Rates.Select(rate => new NbpRateView(code, rate.Mid, rate.EffectiveDate)).ToList();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException
            || (exception is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new NbpUnavailableException("NBP could not be reached.", exception);
        }
    }
}
