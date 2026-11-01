using PSPad.Abstractions;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Module.Investments.Operations;

public sealed record InvestmentOperationRecorded(Guid AggregateId, Guid UserId, DateTimeOffset At, Guid AccountId, OperationKind Kind,
    DateTimeOffset OperationAt, string? Ticker, decimal? Quantity, decimal? Price, MoneyValue Money, string? PositionId,
    OperationSource Source, string? ExternalId, string? Note, TransferType? TransferType = null,
    string? CounterpartyAccountNumber = null, MoneyValue? CounterMoney = null)
    : DomainEvent(AggregateId, UserId, At);
