using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public static class InvestmentAccountAccess
{
    public static InvestmentAccount Owned(InvestmentAccount? account, Guid actorId) =>
        account is null || account.UserId != actorId
            ? throw new DomainRejectedException("That account does not exist.", Unrecoverable: true)
            : account;

    public static InvestmentAccount Writable(InvestmentAccount account) =>
        account.IsArchived ? throw new DomainRejectedException("This account is archived.") : account;
}
