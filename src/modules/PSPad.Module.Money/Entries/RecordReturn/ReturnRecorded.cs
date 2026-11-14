using PSPad.Abstractions;

namespace PSPad.Module.Money.Entries;

public sealed record ReturnRecorded(
    Guid AggregateId, Guid UserId, DateTimeOffset At, Guid BudgetId, Guid PurchaseId, string Name, string Category,
    Money Money, DateOnly Date, string? Note) : DomainEvent(AggregateId, UserId, At);
