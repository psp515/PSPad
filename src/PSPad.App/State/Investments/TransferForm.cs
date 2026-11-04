using PSPad.Module.Investments.Operations;
using PSPad.Module.Money.Values;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.App.State.Investments;

public sealed record TransferForm(TransferType? Type, string? Counterparty, decimal? CounterAmount, string? CounterCurrency)
{
    public static TransferForm Empty(string accountCurrency) =>
        new(null, null, null, accountCurrency == Currencies.Pln ? null : Currencies.Pln);

    public static TransferForm Of(InvestmentOperation operation) =>
        new(operation.TransferType, operation.CounterpartyAccountNumber, operation.CounterMoney?.Amount,
            operation.CounterMoney?.Currency ?? Empty(operation.Money.Currency).CounterCurrency);

    public static bool IsTransfer(OperationKind kind) => kind is OperationKind.TransferIn or OperationKind.TransferOut;

    public static IReadOnlyList<string> CounterCurrencies(string accountCurrency) =>
        [.. Currencies.All.Where(currency => currency != accountCurrency)];

    public bool HasCounter(OperationKind kind) => IsTransfer(kind) && Type == TransferType.CurrencyConversion;

    public bool Complete(OperationKind kind, string accountCurrency) =>
        !IsTransfer(kind)
        || (Type is not null
            && (!HasCounter(kind) || CounterAmount is null
                || (CounterAmount > 0 && CounterCurrency is { } currency && currency != accountCurrency)));

    public (TransferType? Type, string? Counterparty, MoneyValue? Counter) Fields(OperationKind kind, MoneyValue money, DateOnly day) =>
        !IsTransfer(kind)
            ? (null, null, null)
            : (Type,
                string.IsNullOrWhiteSpace(Counterparty) ? null : Counterparty.Trim(),
                HasCounter(kind) && CounterAmount is > 0 && CounterCurrency is { } currency
                    ? ConversionCounter.Of(money, CounterAmount.Value, currency, day)
                    : null);
}
