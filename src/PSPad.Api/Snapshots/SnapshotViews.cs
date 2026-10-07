using PSPad.Contracts;
using PSPad.Module.Sharing.Snapshots;

namespace PSPad.Api.Snapshots;

public static class SnapshotViews
{
    public static SnapshotView From(ListSnapshot snapshot) =>
        new(
            snapshot.Id,
            snapshot.Name,
            snapshot.Kind.ToString(),
            snapshot.CreatedAt,
            snapshot.ExpiresAt,
            snapshot.Tasks.Select(ToTaskView).ToArray(),
            snapshot.Items.Select(ToItemView).ToArray(),
            snapshot.OwnerName);

    public static PublishedSnapshotView ToPublished(ListSnapshot snapshot) =>
        new(snapshot.Id, snapshot.Token, snapshot.CreatedAt, snapshot.ExpiresAt, snapshot.EntryCount, snapshot.TickCount);

    static SnapshotTaskView ToTaskView(SnapshotTask task) =>
        new(
            task.Id,
            task.Name,
            task.Done,
            task.DueOn,
            task.Priority.ToString(),
            task.Starred,
            task.Description,
            task.Marked,
            task.MarkedAt,
            task.Steps.Select(ToStepView).ToArray());

    static SnapshotStepView ToStepView(SnapshotStep step) =>
        new(step.Id, step.Name, step.Done, step.Marked, step.MarkedAt);

    static SnapshotItemView ToItemView(SnapshotItem item) =>
        new(
            item.Id,
            item.Name,
            item.Description,
            item.Starred,
            item.Marked,
            item.MarkedAt,
            item.Fields.Select(field => new SnapshotFieldView(field.Label, field.Value, field.Display)).ToArray());
}
