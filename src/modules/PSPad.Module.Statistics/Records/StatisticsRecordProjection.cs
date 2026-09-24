using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Statistics;

public sealed class StatisticsRecordProjection(IStatisticsStore store) : IDomainEventHandler
{
    public async Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct)
    {
        var record = await BuildAsync(envelope, ct);

        if (record is not null)
        {
            await store.SaveAsync(record, ct);
        }
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
            Id = envelope.Seq,
            UserId = envelope.Event.UserId,
            At = envelope.Event.At,
            Kind = kind,
            TaskId = envelope.Event.AggregateId,
            TaskName = name ?? ""
        };
}
