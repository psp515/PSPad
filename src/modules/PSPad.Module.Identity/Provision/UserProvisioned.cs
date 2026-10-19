using PSPad.Abstractions;

namespace PSPad.Module.Identity;

public sealed record UserProvisioned(
    Guid AggregateId, Guid UserId, DateTimeOffset At, string Subject, string DisplayName, string TimeZone, string Language)
    : DomainEvent(AggregateId, UserId, At);
