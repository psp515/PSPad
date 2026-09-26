namespace PSPad.Module.Statistics;

public interface IInboxRecordStore
{
    Task SaveAsync(InboxRecord record, CancellationToken ct);
    Task<IReadOnlyList<InboxRecord>> SinceAsync(Guid userId, DateTimeOffset from, CancellationToken ct);
}
