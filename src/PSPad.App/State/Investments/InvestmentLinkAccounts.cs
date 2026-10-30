using PSPad.Module.Investments.Accounts;
using PSPad.Module.Money.Balance;

namespace PSPad.App.State.Investments;

public static class InvestmentLinkAccounts
{
    public static IReadOnlyList<LinkRow> Select(
        IEnumerable<InvestmentAccount> accounts, Guid userId, IReadOnlyDictionary<Guid, Holding> links, bool refresh) =>
        [.. Eligible(accounts, userId)
            .Where(account => !refresh || links.ContainsKey(account.Id))
            .OrderBy(account => account.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(account => new LinkRow(account, links.GetValueOrDefault(account.Id)))];

    public static bool CanRefresh(IEnumerable<InvestmentAccount> accounts, Guid userId, IReadOnlyDictionary<Guid, Holding> links) =>
        Eligible(accounts, userId).Any(account => links.ContainsKey(account.Id));

    static IEnumerable<InvestmentAccount> Eligible(IEnumerable<InvestmentAccount> accounts, Guid userId) =>
        accounts.Where(account => !account.Deleted && account.UserId == userId && !account.IsArchived);
}
