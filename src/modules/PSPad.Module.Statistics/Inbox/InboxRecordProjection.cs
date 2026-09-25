using PSPad.Abstractions;
using PSPad.Module.Tasks.Inbox;

namespace PSPad.Module.Statistics;

public sealed class InboxRecordProjection(IInboxRecordStore store) : IDomainEventHandler
{
    public Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct) =>
        envelope.Event switch
        {
            InboxItemCaptured captured =>
                Save(envelope, InboxRecordKind.Captured, captured.ItemId, ct),
            InboxItemOrganised organised =>
                Save(envelope, InboxRecordKind.Organised, organised.ItemId, ct),
            InboxItemDiscarded discarded =>
                Save(envelope, InboxRecordKind.Discarded, discarded.ItemId, ct),
            _ => Task.CompletedTask
        };

    Task Save(DomainEventEnvelope envelope, InboxRecordKind kind, Guid itemId, CancellationToken ct) =>
        store.SaveAsync(
            new InboxRecord
            {
                Id = envelope.Seq,
                UserId = envelope.Event.UserId,
                At = envelope.Event.At,
                Kind = kind,
                ItemId = itemId
            },
            ct);
}
