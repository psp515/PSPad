namespace PSPad.Contracts;

public sealed record PublishedSnapshotView(
    Guid Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, int Entries = 0, int Ticks = 0);
