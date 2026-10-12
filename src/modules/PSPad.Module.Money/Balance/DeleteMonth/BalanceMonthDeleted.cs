using PSPad.Abstractions;

namespace PSPad.Module.Money.Balance;

public sealed record BalanceMonthDeleted(Guid AggregateId, Guid UserId, DateTimeOffset At)
    : DomainEvent(AggregateId, UserId, At);
