namespace PSPad.Module.Sharing.Snapshots;

public sealed record SnapshotStep(Guid Id, string Name, bool Done, bool Marked, DateTimeOffset? MarkedAt);
