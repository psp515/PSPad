using PSPad.Contracts;

namespace PSPad.App.Api;

public interface ISnapshotsApi
{
    Task<PublishedSnapshotView?> PublishAsync(Guid listId, DateTimeOffset expiresAt);

    Task<IReadOnlyList<PublishedSnapshotView>> ForListAsync(Guid listId);

    Task<bool> RevokeAsync(Guid snapshotId);

    Task<bool> RecordVisitAsync(string token);

    Task<IReadOnlyList<SnapshotVisitView>> VisitsAsync();
}
