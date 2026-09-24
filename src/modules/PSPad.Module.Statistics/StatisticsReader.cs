using PSPad.Contracts;

namespace PSPad.Module.Statistics;

public sealed class StatisticsReader(
    IStatisticsStore store, ILabelStore labels, ITaskSnapshotSource snapshots)
{
    const int DefaultLimit = 50;
    const int MaximumLimit = 200;
    const string DeletedTask = "(deleted task)";

    static readonly TaskSnapshot Unknown = new(TaskStatus.Gone, "");

    public async Task<IReadOnlyList<StatisticsRecordView>> ReadAsync(
        Guid userId, long? before, int limit, CancellationToken ct)
    {
        var page = limit switch
        {
            < 1 => DefaultLimit,
            > MaximumLimit => MaximumLimit,
            _ => limit
        };

        var records = await store.PageAsync(userId, before, page, ct);

        if (records.Count == 0)
        {
            return [];
        }

        var current = await snapshots.CurrentAsync(
            records.Select(record => record.TaskId).Distinct().ToArray(), ct);
        var names = (await labels.AllAsync(userId, ct))
            .ToDictionary(label => label.Id, label => label.Name);

        return records.Select(record => View(record, current, names)).ToArray();
    }

    static StatisticsRecordView View(
        StatisticsRecord record,
        IReadOnlyDictionary<Guid, TaskSnapshot> current,
        IReadOnlyDictionary<Guid, string> names)
    {
        var snapshot = current.GetValueOrDefault(record.TaskId, Unknown);

        return new StatisticsRecordView(
            record.Id,
            record.At,
            record.Kind.ToString(),
            record.TaskId,
            NameOf(record, snapshot),
            NameFor(record.ListId, names),
            NameFor(record.GoalId, names),
            record.CompletionNumber,
            snapshot.Status.ToString());
    }

    // An empty TaskName means the record was projected from an event stored before the
    // lifecycle events carried one, so the live task is the only source left for it.
    static string NameOf(StatisticsRecord record, TaskSnapshot snapshot) =>
        !string.IsNullOrEmpty(record.TaskName)
            ? record.TaskName
            : snapshot.Status == TaskStatus.Gone
                ? DeletedTask
                : snapshot.Name;

    static string? NameFor(Guid? id, IReadOnlyDictionary<Guid, string> names) =>
        id is { } value ? names.GetValueOrDefault(value) : null;
}
