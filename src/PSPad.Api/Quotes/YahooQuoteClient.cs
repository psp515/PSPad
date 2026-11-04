using System.Globalization;
using System.Net;
using System.Text.Json;
using PSPad.Abstractions;
using PSPad.Contracts;

namespace PSPad.Api.Quotes;

public sealed class YahooQuoteClient(HttpClient http, IClock clock)
{
    public async Task<YahooChart?> ChartAsync(string symbol, DateOnly from, CancellationToken ct)
    {
        var period1 = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var period2 = clock.UtcNow.ToUnixTimeSeconds();
        var path = string.Create(CultureInfo.InvariantCulture,
            $"v8/finance/chart/{Uri.EscapeDataString(symbol)}?period1={period1}&period2={period2}&interval=1d&events=div");

        try
        {
            using var response = await http.GetAsync(path, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new QuoteUnavailableException($"Yahoo answered {(int)response.StatusCode}.");
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return Parse(document.RootElement);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException
            or InvalidOperationException or FormatException or KeyNotFoundException
            || (exception is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new QuoteUnavailableException("Yahoo could not be reached.", exception);
        }
    }

    static YahooChart? Parse(JsonElement root)
    {
        var chart = root.GetProperty("chart");

        if (chart.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("code", out var code) && code.GetString() == "Not Found")
        {
            return null;
        }

        if (!chart.TryGetProperty("result", out var results) || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0)
        {
            return null;
        }

        var result = results[0];
        var meta = result.GetProperty("meta");
        var currency = meta.GetProperty("currency").GetString()
            ?? throw new QuoteUnavailableException("Yahoo answered without a currency.");
        var offset = meta.TryGetProperty("gmtoffset", out var gmt) && gmt.ValueKind == JsonValueKind.Number ? gmt.GetInt64() : 0;

        var closes = new List<QuoteCloseView>();

        if (result.TryGetProperty("timestamp", out var stamps) && stamps.ValueKind == JsonValueKind.Array)
        {
            var quotes = result.GetProperty("indicators").GetProperty("quote");

            if (quotes.GetArrayLength() == 0)
            {
                throw new QuoteUnavailableException("Yahoo answered without quotes.");
            }

            var values = quotes[0].GetProperty("close");

            for (var index = 0; index < Math.Min(stamps.GetArrayLength(), values.GetArrayLength()); index++)
            {
                var value = values[index];

                if (value.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(stamps[index].GetInt64() + offset).UtcDateTime);
                closes.Add(new QuoteCloseView(date, value.GetDecimal()));
            }
        }

        QuoteLatestView? latest = null;

        if (meta.TryGetProperty("regularMarketPrice", out var price) && price.ValueKind == JsonValueKind.Number
            && meta.TryGetProperty("regularMarketTime", out var time) && time.ValueKind == JsonValueKind.Number)
        {
            latest = new QuoteLatestView(DateTimeOffset.FromUnixTimeSeconds(time.GetInt64()), price.GetDecimal());
        }

        return new YahooChart(currency, closes, latest);
    }
}
