using PSPad.Abstractions;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Module.Investments.Operations;

public sealed record EditInvestmentOperation(Guid CommandId, Guid UserId, Guid OperationId, OperationKind Kind, DateTimeOffset At,
    string? Ticker, decimal? Quantity, decimal? Price, MoneyValue Money, string? PositionId, string? Note) : ICommand;
