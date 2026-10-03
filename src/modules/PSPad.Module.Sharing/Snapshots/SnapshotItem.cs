namespace PSPad.Module.Sharing.Snapshots;

public sealed record SnapshotItem(
    Guid Id, string Name, string Description, bool Starred, bool Marked,
    DateTimeOffset? MarkedAt, IReadOnlyList<SnapshotField> Fields);
