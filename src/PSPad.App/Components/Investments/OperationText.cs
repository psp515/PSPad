using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.App.State.Investments;
using PSPad.Module.Investments.Operations;

namespace PSPad.App.Components.Investments;

public sealed record OperationText(string Kind, string Title, string Sub)
{
    public static OperationText For(
        InvestmentOperation operation, string accountName, string? instrumentName, IStringLocalizer<InvestmentsStrings> text)
    {
        var kind = text[OperationKinds.Key(operation.Kind)].Value;
        var title = operation.Ticker is { } ticker
            ? instrumentName is { } name && name != ticker ? $"{kind} {ticker} · {name}" : $"{kind} {ticker}"
            : kind;
        var sub = string.Join(" · ", new[]
        {
            operation is { Quantity: { } quantity, Price: { } price }
                ? $"{InvestmentText.Quantity(quantity)} @ {InvestmentText.Price(price)}"
                : null,
            operation.TransferType is { } transfer ? text[OperationKinds.TransferKey(transfer)].Value : null,
            operation.CounterpartyAccountNumber,
            operation.CounterMoney is { } counter ? InvestmentText.Money(counter.Amount, counter.Currency) : null,
            operation.Note,
            accountName,
            operation.Source == OperationSource.Xtb ? text["Imported"].Value : null,
            operation.Deleted ? text["Deleted"].Value : null
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return new OperationText(kind, title, sub);
    }
}
