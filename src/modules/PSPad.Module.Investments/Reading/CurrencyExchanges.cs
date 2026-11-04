using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

public static class CurrencyExchanges
{
    const string Pln = "PLN";

    public static IReadOnlyList<CurrencyExchange> For(
        IReadOnlyList<InvestmentAccount> accounts, IReadOnlyList<InvestmentOperation> operations, FxBook fx, DateOnly today,
        TimeZoneInfo zone, Guid? onlyAccount = null)
    {
        var kept = accounts.Where(account => !account.Deleted).ToList();
        var live = kept.Select(account => (Account: account,
                Live: LiveOperations.Of(operations.Where(operation => operation.AccountId == account.Id))))
            .ToList();
        var paired = InternalTransfers.Among(live);
        var legs = live.Where(entry => onlyAccount is null || entry.Account.Id == onlyAccount)
            .SelectMany(entry => entry.Live.Select(operation => LegOf(entry.Account, operation, paired, onlyAccount is not null)))
            .OfType<Leg>()
            .ToList();
        var pools = live.Where(entry => !IsPln(entry.Account.Currency))
            .GroupBy(entry => entry.Account.Currency, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key,
                group => group.Select(entry => FxCost.Of(entry.Account.Currency, entry.Live)).ToList(),
                StringComparer.OrdinalIgnoreCase);

        return [.. legs.GroupBy(leg => leg.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var bought = group.Where(leg => leg.Bought).ToList();
                var sold = group.Where(leg => !leg.Bought).ToList();
                var pool = pools.GetValueOrDefault(group.Key) is { } costs && costs.Sum(cost => cost.Units) > 0 ? costs : null;
                var days = group.Select(leg => LiveOperations.DayOf(leg.Operation, zone)).ToList();
                return new CurrencyExchange(group.Key.ToUpperInvariant(),
                    bought.Sum(leg => leg.Units), bought.Where(leg => leg.Pln is not null).Sum(leg => leg.Units),
                    bought.Sum(leg => leg.Pln ?? 0m),
                    sold.Sum(leg => leg.Units), sold.Where(leg => leg.Pln is not null).Sum(leg => leg.Units),
                    sold.Sum(leg => leg.Pln ?? 0m),
                    pool?.Sum(cost => cost.Units), pool?.Sum(cost => cost.PoolPln), fx.ToPln(group.Key, today),
                    group.Count(), days.Min(), days.Max());
            })
            .OrderBy(exchange => exchange.Currency, StringComparer.Ordinal)];
    }

    static Leg? LegOf(InvestmentAccount account, InvestmentOperation operation, IReadOnlySet<Guid> paired, bool oneAccount)
    {
        if (operation is not { TransferType: TransferType.CurrencyConversion, CounterMoney: { } counter }
            || operation.Kind is not (OperationKind.TransferIn or OperationKind.TransferOut))
        {
            return null;
        }

        var incoming = operation.Kind == OperationKind.TransferIn;
        if (!IsPln(account.Currency))
        {
            return new Leg(operation, account.Currency, incoming, operation.Money.Amount, IsPln(counter.Currency) ? counter.Amount : null);
        }

        return IsPln(counter.Currency) || (!oneAccount && paired.Contains(operation.Id))
            ? null
            : new Leg(operation, counter.Currency, !incoming, counter.Amount, operation.Money.Amount);
    }

    static bool IsPln(string currency) => string.Equals(currency, Pln, StringComparison.OrdinalIgnoreCase);

    sealed record Leg(InvestmentOperation Operation, string Currency, bool Bought, decimal Units, decimal? Pln);
}
