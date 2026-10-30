using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Values;

namespace PSPad.App.State.Investments;

public sealed record InvestmentLinkState(bool MonthExists, IReadOnlyDictionary<Guid, Holding> Links)
{
    public static InvestmentLinkState For(IEnumerable<BalanceSnapshot> budgetSnapshots, YearMonth month)
    {
        var live = budgetSnapshots.Where(snapshot => !snapshot.Deleted).ToList();
        var own = live.FirstOrDefault(snapshot => snapshot.Month == month);
        var source = own ?? live.Where(snapshot => snapshot.Month < month).OrderByDescending(snapshot => snapshot.Month).FirstOrDefault();

        var links = source is null
            ? []
            : source.Holdings.Where(holding => holding.InvestmentAccountId is not null)
                .GroupBy(holding => holding.InvestmentAccountId!.Value)
                .ToDictionary(group => group.Key, group => group.First());

        return new InvestmentLinkState(own is not null, links);
    }
}
