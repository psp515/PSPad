using PSPad.Abstractions;

namespace PSPad.Module.Identity;

public sealed record UserDisplayNameSet(
    Guid AggregateId, Guid UserId, DateTimeOffset At, string DisplayName)
    : DomainEvent(AggregateId, UserId, At);
