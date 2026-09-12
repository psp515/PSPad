namespace PSPad.Infrastructure.Mongo;

public sealed class StoredEvent
{
    public long Seq { get; init; }
    public Guid UserId { get; init; }
    public string AggregateType { get; init; } = "";
    public Guid AggregateId { get; init; }
    public string Type { get; init; } = "";
    public string Payload { get; init; } = "";
    public DateTimeOffset At { get; init; }
}
