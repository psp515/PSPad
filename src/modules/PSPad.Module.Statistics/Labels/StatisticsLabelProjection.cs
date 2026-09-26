using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Lists;

namespace PSPad.Module.Statistics;

public sealed class StatisticsLabelProjection(ILabelStore store) : IDomainEventHandler
{
    public Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct) =>
        envelope.Event switch
        {
            AreaCreated created => Create(created.AggregateId, created.UserId, LabelKind.Area, created.Name, ct),
            AreaRenamed renamed => Rename(renamed.AggregateId, renamed.Name, ct),
            AreaDeleted deleted => Delete(deleted.AggregateId, ct),
            TaskListCreated created => Create(created.AggregateId, created.UserId, LabelKind.List, created.Name, ct),
            TaskListRenamed renamed => Rename(renamed.AggregateId, renamed.Name, ct),
            TaskListDeleted deleted => Delete(deleted.AggregateId, ct),
            GoalCreated created => Create(created.AggregateId, created.UserId, LabelKind.Goal, created.Name, ct),
            GoalRenamed renamed => Rename(renamed.AggregateId, renamed.Name, ct),
            GoalDeleted deleted => Delete(deleted.AggregateId, ct),
            _ => Task.CompletedTask
        };

    Task Create(Guid id, Guid userId, LabelKind kind, string name, CancellationToken ct) =>
        store.SaveAsync(new StatisticsLabel { Id = id, UserId = userId, Kind = kind, Name = name }, ct);

    async Task Rename(Guid id, string name, CancellationToken ct)
    {
        var existing = await store.FindAsync(id, ct);

        if (existing is not null)
        {
            await store.SaveAsync(existing with { Name = name }, ct);
        }
    }

    async Task Delete(Guid id, CancellationToken ct)
    {
        var existing = await store.FindAsync(id, ct);

        if (existing is not null)
        {
            await store.SaveAsync(existing with { Deleted = true }, ct);
        }
    }
}
