namespace PSPad.Contracts;

public sealed record HistoryEntry(
    long Seq, DateTimeOffset At, string AggregateType, Guid AggregateId, string Description);
