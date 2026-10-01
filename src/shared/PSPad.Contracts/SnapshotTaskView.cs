namespace PSPad.Contracts;

public sealed record SnapshotTaskView(
    Guid Id,
    string Name,
    bool Done,
    DateOnly? DueOn,
    string Priority,
    bool Starred,
    string Description,
    bool Marked,
    DateTimeOffset? MarkedAt,
    SnapshotStepView[] Steps);
