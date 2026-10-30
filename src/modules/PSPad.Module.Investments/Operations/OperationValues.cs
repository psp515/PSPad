using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Module.Investments.Operations;

internal sealed record OperationValues(
    OperationKind Kind, DateTimeOffset At, string? Ticker, decimal? Quantity, decimal? Price, MoneyValue Money,
    string? PositionId, string? Note);
