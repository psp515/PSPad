using PSPad.Api.Identity;
using PSPad.Api.Sync;

namespace PSPad.Api.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("sync", async (
            long since, string? full, SyncReader reader, ICurrentUser user, CancellationToken ct) =>
            Results.Ok(await reader.ReadAsync(user.UserId, since, ListIds(full), ct)));
    }

    static Guid[] ListIds(string? full) =>
        (full ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Guid.TryParse(part, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
}
