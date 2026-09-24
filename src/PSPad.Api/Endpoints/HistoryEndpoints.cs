using PSPad.Api.Identity;
using PSPad.Module.Statistics;

namespace PSPad.Api.Endpoints;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("history", async (
            long? before,
            int? limit,
            HistoryReader reader,
            ICurrentUser current,
            CancellationToken ct) =>
            Results.Ok(await reader.ReadAsync(current.UserId, before, limit ?? 50, ct)));
    }
}
