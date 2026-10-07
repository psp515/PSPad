namespace PSPad.Contracts;

public sealed record SnapshotView(
    Guid Id,
    string Name,
    string Kind,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    SnapshotTaskView[] Tasks,
    SnapshotItemView[] Items,
    string OwnerName = "");
