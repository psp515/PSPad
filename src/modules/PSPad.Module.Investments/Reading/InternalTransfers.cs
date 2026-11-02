using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Module.Investments.Reading;

public static class InternalTransfers
{
    const decimal CounterTolerance = 0.05m;

    static readonly TimeSpan PairingWindow = TimeSpan.FromDays(1);

    public static IReadOnlySet<Guid> Among(IEnumerable<(InvestmentAccount Account, IReadOnlyList<InvestmentOperation> Live)> accounts)
    {
        var all = accounts.ToList();
        var numberOf = all.Where(entry => !string.IsNullOrWhiteSpace(entry.Account.BrokerAccountNumber))
            .ToDictionary(entry => entry.Account.Id, entry => entry.Account.BrokerAccountNumber!.Trim());
        var legs = LiveOperations.Of(all.SelectMany(entry => entry.Live))
            .Where(operation => operation.TransferType is not null
                && operation.Kind is OperationKind.TransferIn or OperationKind.TransferOut)
            .ToList();
        var ins = legs.Where(operation => operation.Kind == OperationKind.TransferIn).ToList();
        var paired = new HashSet<Guid>();

        foreach (var leg in legs.Where(operation => operation.Kind == OperationKind.TransferOut))
        {
            var partner = ins.Where(other => !paired.Contains(other.Id) && OneTransfer(leg, other, numberOf))
                .OrderBy(other => (other.At - leg.At).Duration())
                .FirstOrDefault();
            if (partner is not null)
            {
                paired.Add(leg.Id);
                paired.Add(partner.Id);
            }
        }

        return paired;
    }

    static bool OneTransfer(InvestmentOperation outLeg, InvestmentOperation inLeg, IReadOnlyDictionary<Guid, string> numberOf) =>
        outLeg.AccountId != inLeg.AccountId
        && outLeg.TransferType == inLeg.TransferType
        && (inLeg.At - outLeg.At).Duration() <= PairingWindow
        && AmountsMatch(outLeg, inLeg)
        && (outLeg.TransferType == TransferType.Ike
            || Names(outLeg, inLeg.AccountId, numberOf)
            || Names(inLeg, outLeg.AccountId, numberOf));

    static bool Names(InvestmentOperation leg, Guid accountId, IReadOnlyDictionary<Guid, string> numberOf) =>
        leg.CounterpartyAccountNumber is { } counterparty && numberOf.TryGetValue(accountId, out var number)
        && counterparty.Trim() == number;

    static bool AmountsMatch(InvestmentOperation outLeg, InvestmentOperation inLeg)
    {
        if (SameCurrency(outLeg.Money, inLeg.Money))
        {
            return outLeg.Money.Amount == inLeg.Money.Amount;
        }

        if (outLeg.CounterMoney is null && inLeg.CounterMoney is null)
        {
            return true;
        }

        return Close(outLeg.CounterMoney, inLeg.Money) || Close(inLeg.CounterMoney, outLeg.Money);
    }

    static bool Close(MoneyValue? counter, MoneyValue money) =>
        counter is not null && SameCurrency(counter, money) && Math.Abs(counter.Amount - money.Amount) <= CounterTolerance;

    static bool SameCurrency(MoneyValue one, MoneyValue other) =>
        string.Equals(one.Currency, other.Currency, StringComparison.OrdinalIgnoreCase);
}
