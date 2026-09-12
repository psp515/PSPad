namespace PSPad.Contracts;

public sealed record RecordedEvent(
    long Seq, string AggregateType, Guid AggregateId, string Type, string Payload, DateTimeOffset At);
