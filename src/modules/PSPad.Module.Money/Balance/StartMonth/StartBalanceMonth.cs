using PSPad.Abstractions;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Balance;

public sealed record StartBalanceMonth(Guid CommandId, Guid UserId, Guid BudgetId, YearMonth Month) : ICommand;
