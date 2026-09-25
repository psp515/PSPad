namespace PSPad.Module.Statistics;

public sealed record InboxRecord
{
    public long Id { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset At { get; init; }
    public InboxRecordKind Kind { get; init; }
    public Guid ItemId { get; init; }
}
