using PSPad.Abstractions;

namespace PSPad.Module.Identity;

public sealed record UserLanguageSet(Guid AggregateId, Guid UserId, DateTimeOffset At, string Language)
    : DomainEvent(AggregateId, UserId, At);
