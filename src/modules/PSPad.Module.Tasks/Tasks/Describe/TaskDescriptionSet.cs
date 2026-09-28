using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskDescriptionSet(Guid AggregateId, Guid UserId, DateTimeOffset At, string Description)
    : DomainEvent(AggregateId, UserId, At);
