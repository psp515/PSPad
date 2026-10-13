using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record BudgetJoined(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid MemberId, string DisplayName)
    : DomainEvent(AggregateId, UserId, At);
