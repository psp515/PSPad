using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

internal static class InternalTransfers
{
    static readonly TimeSpan IkePairingWindow = TimeSpan.FromDays(1);

    public static IReadOnlySet<Guid> Among(IEnumerable<(InvestmentAccount Account, IReadOnlyList<InvestmentOperation> Live)> accounts)
    {
        var all = accounts.ToList();
        var numbers = all.Where(entry => !string.IsNullOrWhiteSpace(entry.Account.BrokerAccountNumber))
            .Select(entry => (Number: entry.Account.BrokerAccountNumber!.Trim(), entry.Account.Id))
            .ToList();
        var transfers = all.SelectMany(entry => entry.Live)
            .Where(operation => operation.Kind is OperationKind.TransferIn or OperationKind.TransferOut)
            .ToList();

        var internalIds = transfers
            .Where(operation => operation.CounterpartyAccountNumber is { } counterparty
                && numbers.Any(own => own.Id != operation.AccountId && own.Number == counterparty.Trim()))
            .Select(operation => operation.Id)
            .ToHashSet();

        var ike = transfers.Where(operation => operation.TransferType == TransferType.Ike && !internalIds.Contains(operation.Id)).ToList();
        var ins = ike.Where(operation => operation.Kind == OperationKind.TransferIn).ToList();
        foreach (var leg in ike.Where(operation => operation.Kind == OperationKind.TransferOut).OrderBy(operation => operation.At)
                     .ThenBy(operation => operation.Id))
        {
            var partner = ins.Where(other => other.AccountId != leg.AccountId && !internalIds.Contains(other.Id)
                    && other.Money.Amount == leg.Money.Amount
                    && string.Equals(other.Money.Currency, leg.Money.Currency, StringComparison.OrdinalIgnoreCase)
                    && (other.At - leg.At).Duration() <= IkePairingWindow)
                .OrderBy(other => (other.At - leg.At).Duration())
                .ThenBy(other => other.Id)
                .FirstOrDefault();
            if (partner is not null)
            {
                internalIds.Add(leg.Id);
                internalIds.Add(partner.Id);
            }
        }

        return internalIds;
    }
}
