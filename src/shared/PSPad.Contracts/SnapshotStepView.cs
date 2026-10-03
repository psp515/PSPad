namespace PSPad.Contracts;

public sealed record SnapshotStepView(Guid Id, string Name, bool Done, bool Marked, DateTimeOffset? MarkedAt);
