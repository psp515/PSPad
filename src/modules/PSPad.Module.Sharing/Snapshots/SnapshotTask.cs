using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Sharing.Snapshots;

public sealed record SnapshotTask(
    Guid Id, string Name, bool Done, DateOnly? DueOn, Priority Priority, bool Starred,
    string Description, bool Marked, DateTimeOffset? MarkedAt, IReadOnlyList<SnapshotStep> Steps);
