using PSPad.Api.Identity;
using PSPad.Api.Sync;

namespace PSPad.Api.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sync", async (
            long since, SyncReader reader, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(await reader.ReadAsync(user.UserId, since, ct)));
    }
}
