using PSPad.Abstractions;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Balance;

public sealed record BalanceMonthStarted(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid BudgetId, YearMonth Month, IReadOnlyList<Holding> Holdings)
    : DomainEvent(AggregateId, UserId, At);
