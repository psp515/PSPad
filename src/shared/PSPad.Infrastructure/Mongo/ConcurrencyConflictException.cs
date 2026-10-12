namespace PSPad.Infrastructure.Mongo;

public sealed class ConcurrencyConflictException(Guid aggregateId)
    : Exception($"Aggregate {aggregateId} changed after it was loaded.")
{
    public Guid AggregateId { get; } = aggregateId;
}
