using PSPad.Contracts;

namespace PSPad.App.State;

public static class SnapshotVisitRows
{
    public static IReadOnlyList<SnapshotVisitView> Live(IEnumerable<SnapshotVisitView> rows, DateTimeOffset now) =>
        [.. rows.Where(row => row.ExpiresAt > now).OrderByDescending(row => row.VisitedAt)];

    public static IReadOnlyList<SnapshotVisitView> FromCache(
        IEnumerable<(string Token, SnapshotView Snapshot, DateTimeOffset OpenedAt)> cached) =>
        [.. cached.Select(entry => new SnapshotVisitView(
            entry.Token, entry.Snapshot.Name, entry.Snapshot.ExpiresAt, entry.OpenedAt))];
}
