using PSPad.Abstractions;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Balance;

public sealed record AddHolding(
    Guid CommandId, Guid UserId, Guid BudgetId, YearMonth Month, Guid HoldingId, string Name, HoldingType Type,
    Money Money) : ICommand;
