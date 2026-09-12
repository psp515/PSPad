using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed record TaskStarred(Guid AggregateId, Guid UserId, DateTimeOffset At, bool Starred)
    : DomainEvent(AggregateId, UserId, At);
