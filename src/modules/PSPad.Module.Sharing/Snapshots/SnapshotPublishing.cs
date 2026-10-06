using PSPad.Abstractions;
using PSPad.Module.Sharing.Ports;

namespace PSPad.Module.Sharing.Snapshots;

public sealed class SnapshotPublishing(ISnapshotStore snapshots, IListContent content, IClock clock)
{
    public static readonly TimeSpan LongestLife = TimeSpan.FromDays(365);

    public async Task<(PublishOutcome Outcome, ListSnapshot? Snapshot)> PublishAsync(
        Guid userId, string ownerName, Guid listId, DateTimeOffset expiresAt, DateOnly today, CancellationToken ct)
    {
        var (list, tasks, items) = await content.LoadAsync(listId, ct);
        if (list is null || list.Deleted)
        {
            return (PublishOutcome.NotFound, null);
        }

        if (list.UserId != userId)
        {
            return (PublishOutcome.NotOwner, null);
        }

        var now = clock.UtcNow;
        if (expiresAt <= now || expiresAt > now + LongestLife)
        {
            return (PublishOutcome.BadExpiry, null);
        }

        var snapshot = SnapshotBuilder.Build(
            Guid.NewGuid(), SnapshotTokens.New(), list, tasks, items, now, expiresAt, today, ownerName);
        await snapshots.SaveAsync(snapshot, ct);
        return (PublishOutcome.Published, snapshot);
    }

    public async Task<bool> RevokeAsync(Guid userId, Guid snapshotId, CancellationToken ct)
    {
        var snapshot = await snapshots.FindAsync(snapshotId, ct);
        if (snapshot is null || snapshot.UserId != userId)
        {
            return false;
        }

        await snapshots.DeleteAsync(snapshotId, ct);
        return true;
    }

    public async Task<IReadOnlyList<ListSnapshot>> ActiveAsync(Guid userId, Guid listId, CancellationToken ct)
    {
        var all = await snapshots.ForListAsync(userId, listId, ct);
        var now = clock.UtcNow;
        return all.Where(snapshot => snapshot.IsLiveAt(now))
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .ToArray();
    }
}
