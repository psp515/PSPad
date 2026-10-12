using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record CategoriesMerged(
    Guid AggregateId, Guid UserId, DateTimeOffset At, CategoryKind Kind, string From, string Into)
    : DomainEvent(AggregateId, UserId, At);
