using PSPad.Abstractions;

namespace PSPad.Module.Identity;

public sealed record UserTimeZoneSet(Guid AggregateId, Guid UserId, DateTimeOffset At, string TimeZone)
    : DomainEvent(AggregateId, UserId, At);
