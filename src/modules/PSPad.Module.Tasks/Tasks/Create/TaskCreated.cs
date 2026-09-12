using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskCreated(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid ListId, string Name)
    : DomainEvent(AggregateId, UserId, At);
