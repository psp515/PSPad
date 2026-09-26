using PSPad.Abstractions;
using PSPad.Module.Tasks.Inbox;

namespace PSPad.Module.Statistics;

public sealed class InboxRecordProjection(IInboxRecordStore store) : IDomainEventHandler
{
    public Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct) =>
        envelope.Event is InboxItemCaptured captured
            ? store.SaveAsync(
                new InboxRecord
                {
                    Id = envelope.Seq,
                    UserId = captured.UserId,
                    At = captured.At,
                    ItemId = captured.ItemId
                },
                ct)
            : Task.CompletedTask;
}
