namespace PSPad.Abstractions;

public abstract record DomainEvent(Guid AggregateId, Guid UserId, DateTimeOffset At);
