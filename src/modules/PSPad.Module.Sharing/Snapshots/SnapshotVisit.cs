namespace PSPad.Module.Sharing.Snapshots;

public sealed record SnapshotVisit(
    string Id, Guid UserId, Guid SnapshotId, string Token, string Name,
    DateTimeOffset ExpiresAt, DateTimeOffset VisitedAt)
{
    public static string IdFor(Guid userId, Guid snapshotId) => $"{userId:N}:{snapshotId:N}";
}
