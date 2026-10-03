namespace PSPad.Contracts;

public sealed record PublishedSnapshotView(Guid Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
