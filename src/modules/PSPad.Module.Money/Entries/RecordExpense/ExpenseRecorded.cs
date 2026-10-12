using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record ExpenseRecorded(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid BudgetId, string Name, string Category, Money Money,
    DateOnly Date, string? Note) : DomainEvent(AggregateId, UserId, At);
