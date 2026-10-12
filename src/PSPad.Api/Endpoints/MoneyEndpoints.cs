using PSPad.Api.Rates;

namespace PSPad.Api.Endpoints;

public static class MoneyEndpoints
{
    public static void MapMoneyEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("money/nbp-rate", async (string currency, DateOnly date, NbpRateClient nbp, CancellationToken ct) =>
        {
            try
            {
                var rate = await nbp.RateAsync(currency, date, ct);
                return rate is null ? Results.NotFound() : Results.Ok(rate);
            }
            catch (NbpUnavailableException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        });
}
