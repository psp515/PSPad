using PSPad.Abstractions;
using PSPad.Api.Snapshots;
using PSPad.Contracts;
using PSPad.Module.Sharing.Ports;
using PSPad.Module.Sharing.Snapshots;

namespace PSPad.Api.Endpoints;

public static class PublicSnapshotEndpoints
{
    public static void MapPublicSnapshotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("snapshots/{token}", async (
            string token, ISnapshotStore snapshots, IClock clock, CancellationToken ct) =>
        {
            var snapshot = await snapshots.FindByTokenAsync(token, ct);
            return snapshot is not null && snapshot.IsLiveAt(clock.UtcNow)
                ? Results.Ok(SnapshotViews.From(snapshot))
                : Results.NotFound();
        });

        app.MapPost("snapshots/{token}/marks", async (
            string token, MarkSnapshotEntryRequest request, SnapshotMarking marking, CancellationToken ct) =>
        {
            var outcome = await marking.MarkAsync(token, request.EntryId, request.StepId, request.Marked, ct);
            return outcome == MarkOutcome.NotFound ? Results.NotFound() : Results.NoContent();
        });
    }
}
