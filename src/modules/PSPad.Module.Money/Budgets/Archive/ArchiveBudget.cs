using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed record ArchiveBudget(Guid CommandId, Guid UserId, Guid BudgetId) : ICommand;
