using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed record AccountOperationCounted(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
