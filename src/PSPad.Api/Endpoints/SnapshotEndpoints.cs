using PSPad.Abstractions;
using PSPad.Api.Identity;
using PSPad.Api.Snapshots;
using PSPad.Contracts;
using PSPad.Module.Identity;
using PSPad.Module.Sharing.Ports;
using PSPad.Module.Sharing.Snapshots;
using PSPad.Module.Tasks.Today;

namespace PSPad.Api.Endpoints;

public static class SnapshotEndpoints
{
    public static void MapSnapshotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("lists/{id:guid}/snapshots", async (
            Guid id,
            PublishSnapshotRequest request,
            SnapshotPublishing publishing,
            IDocumentStore<User> users,
            ICurrentUser current,
            IClock clock,
            CancellationToken ct) =>
        {
            var user = await users.LoadAsync(current.UserId, ct);
            var zone = TimeZoneInfo.FindSystemTimeZoneById(user?.TimeZone ?? "Etc/UTC");
            var today = TodayRule.TodayIn(clock.UtcNow, zone);

            var (outcome, snapshot) = await publishing.PublishAsync(current.UserId, current.DisplayName, id, request.ExpiresAt, today, ct);

            return outcome switch
            {
                PublishOutcome.Published => Results.Ok(SnapshotViews.ToPublished(snapshot!)),
                PublishOutcome.BadExpiry => Results.BadRequest(),
                _ => Results.NotFound()
            };
        });

        app.MapGet("lists/{id:guid}/snapshots", async (
            Guid id, SnapshotPublishing publishing, ICurrentUser current, CancellationToken ct) =>
        {
            var snapshots = await publishing.ActiveAsync(current.UserId, id, ct);
            return Results.Ok(snapshots.Select(SnapshotViews.ToPublished).ToArray());
        });

        app.MapDelete("snapshots/{id:guid}", async (
            Guid id, SnapshotPublishing publishing, ICurrentUser current, CancellationToken ct) =>
        {
            var revoked = await publishing.RevokeAsync(current.UserId, id, ct);
            return revoked ? Results.NoContent() : Results.NotFound();
        });

        app.MapPost("me/snapshot-visits", async (
            RecordVisitRequest request,
            ISnapshotStore snapshots,
            ISnapshotVisitStore visits,
            ICurrentUser current,
            IClock clock,
            CancellationToken ct) =>
        {
            var snapshot = await snapshots.FindByTokenAsync(request.Token, ct);
            var now = clock.UtcNow;
            if (snapshot is null || !snapshot.IsLiveAt(now))
            {
                return Results.NotFound();
            }

            var visit = new SnapshotVisit(
                SnapshotVisit.IdFor(current.UserId, snapshot.Id),
                current.UserId,
                snapshot.Id,
                snapshot.Token,
                snapshot.Name,
                snapshot.ExpiresAt,
                now);

            await visits.SaveAsync(visit, ct);
            return Results.NoContent();
        });

        app.MapGet("me/snapshot-visits", async (
            ISnapshotVisitStore visits, ICurrentUser current, IClock clock, CancellationToken ct) =>
        {
            var now = clock.UtcNow;
            var all = await visits.ForUserAsync(current.UserId, ct);
            var live = all
                .Where(visit => visit.ExpiresAt > now)
                .OrderByDescending(visit => visit.VisitedAt)
                .Select(visit => new SnapshotVisitView(visit.Token, visit.Name, visit.ExpiresAt, visit.VisitedAt))
                .ToArray();

            return Results.Ok(live);
        });
    }
}
