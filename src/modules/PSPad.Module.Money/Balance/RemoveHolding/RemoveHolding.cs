using PSPad.Abstractions;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Balance;

public sealed record RemoveHolding(Guid CommandId, Guid UserId, Guid BudgetId, YearMonth Month, Guid HoldingId)
    : ICommand;
