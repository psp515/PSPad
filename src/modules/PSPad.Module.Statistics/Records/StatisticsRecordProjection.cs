using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Statistics;

public sealed class StatisticsRecordProjection(IStatisticsStore store) : IDomainEventHandler
{
    public async Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct)
    {
        var record = await BuildAsync(envelope, ct);

        if (record is null)
        {
            return;
        }

        await store.SaveAsync(record, ct);

        if (envelope.Event.Actor != envelope.Event.UserId)
        {
            await store.SaveAsync(await AsActorAsync(record, envelope, ct), ct);
        }
    }

    async Task<StatisticsRecord> AsActorAsync(StatisticsRecord owner, DomainEventEnvelope envelope, CancellationToken ct)
    {
        var actor = envelope.Event.Actor;

        return owner with
        {
            Id = StatisticsRecord.IdFor(envelope.Seq, actor),
            UserId = actor,
            Role = RecordRole.Actor,
            GoalId = null,
            CompletionNumber = owner.Kind == RecordKind.Completed
                ? await store.CountCompletionsBeforeAsync(actor, owner.TaskId, envelope.Seq, ct) + 1
                : owner.CompletionNumber
        };
    }

    async Task<StatisticsRecord?> BuildAsync(DomainEventEnvelope envelope, CancellationToken ct) =>
        envelope.Event switch
        {
            TaskCreated created => Base(envelope, RecordKind.Created, created.Name) with
            {
                ListId = created.ListId
            },
            TaskCompleted completed => Base(envelope, RecordKind.Completed, completed.Name) with
            {
                ListId = completed.ListId,
                GoalId = completed.GoalId,
                DueOn = completed.DueOn,
                CompletionNumber = await store.CountCompletionsBeforeAsync(
                    completed.UserId, completed.AggregateId, envelope.Seq, ct) + 1
            },
            TaskReopened reopened => Base(envelope, RecordKind.Reopened, reopened.Name) with
            {
                ListId = reopened.ListId
            },
            TaskDeleted deleted => Base(envelope, RecordKind.Deleted, deleted.Name),
            TaskMovedToList moved => Base(envelope, RecordKind.Moved, moved.Name) with
            {
                ListId = moved.ListId
            },
            TaskLinkedToGoal linked => Base(envelope, RecordKind.LinkedToGoal, linked.Name) with
            {
                GoalId = linked.GoalId
            },
            OccurrenceCompleted ticked => Base(
                envelope,
                ticked.Completed ? RecordKind.OccurrenceTicked : RecordKind.OccurrenceUnticked,
                ticked.Name) with
            {
                ListId = ticked.ListId,
                GoalId = ticked.GoalId,
                OccurrenceDay = ticked.Day
            },
            _ => null
        };

    static StatisticsRecord Base(DomainEventEnvelope envelope, RecordKind kind, string name) =>
        new()
        {
            Id = StatisticsRecord.IdFor(envelope.Seq, envelope.Event.UserId),
            Seq = envelope.Seq,
            Role = RecordRole.Owner,
            UserId = envelope.Event.UserId,
            At = envelope.Event.At,
            Kind = kind,
            TaskId = envelope.Event.AggregateId,
            TaskName = name ?? ""
        };
}
