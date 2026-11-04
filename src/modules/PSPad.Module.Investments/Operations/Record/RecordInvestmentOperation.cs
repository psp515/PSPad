using PSPad.Abstractions;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Module.Investments.Operations;

public sealed record RecordInvestmentOperation(Guid CommandId, Guid UserId, Guid OperationId, Guid AccountId, OperationKind Kind,
    DateTimeOffset At, string? Ticker, decimal? Quantity, decimal? Price, MoneyValue Money, string? PositionId,
    OperationSource Source, string? ExternalId, string? Note, TransferType? TransferType = null,
    string? CounterpartyAccountNumber = null, MoneyValue? CounterMoney = null) : ICommand;
