namespace PSPad.Contracts;

public sealed record SnapshotItemView(
    Guid Id,
    string Name,
    string Description,
    bool Starred,
    bool Marked,
    DateTimeOffset? MarkedAt,
    SnapshotFieldView[] Fields);
