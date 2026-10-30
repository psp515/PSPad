using System.Text.RegularExpressions;
using PSPad.Abstractions;
using PSPad.Api.Quotes;

namespace PSPad.Api.Endpoints;

public static partial class InvestmentEndpoints
{
    static readonly DateOnly FirstDate = new(1970, 1, 2);

    public static void MapInvestmentEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("investments/quotes", async (
            string symbol, DateOnly from, bool? refresh, QuoteCache quotes, IClock clock, CancellationToken ct) =>
        {
            var trimmed = symbol.Trim();

            if (!ValidSymbol().IsMatch(trimmed) || from < FirstDate || from > DateOnly.FromDateTime(clock.UtcNow.UtcDateTime))
            {
                return Results.NotFound();
            }

            try
            {
                var series = await quotes.SeriesAsync(trimmed, from, refresh ?? false, ct);
                return series is null ? Results.NotFound() : Results.Ok(series);
            }
            catch (QuoteUnavailableException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        }).RequireRateLimiting("quotes");

    [GeneratedRegex(@"^(?=.*[A-Za-z0-9])[A-Za-z0-9.\-^=]{1,24}$")]
    private static partial Regex ValidSymbol();
}
