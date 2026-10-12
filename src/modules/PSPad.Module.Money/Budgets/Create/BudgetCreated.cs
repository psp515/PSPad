using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record BudgetCreated(
    Guid AggregateId, Guid UserId, DateTimeOffset At, string Name,
    IReadOnlyList<string> ExpenseCategories, IReadOnlyList<string> IncomeCategories)
    : DomainEvent(AggregateId, UserId, At);
