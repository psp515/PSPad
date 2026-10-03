namespace PSPad.Contracts;

public sealed record SnapshotVisitView(string Token, string Name, DateTimeOffset ExpiresAt, DateTimeOffset VisitedAt);
