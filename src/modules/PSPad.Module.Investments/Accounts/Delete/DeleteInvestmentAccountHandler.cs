using PSPad.Abstractions;
using PSPad.Module.Investments.Portfolio;

namespace PSPad.Module.Investments.Accounts;

public sealed class DeleteInvestmentAccountHandler(
    IDocumentStore<InvestmentAccount> accounts, IDocumentStore<InvestmentPortfolio> portfolios, IUnitOfWork work,
    IClock clock)
    : ICommandHandler<DeleteInvestmentAccount>
{
    public async Task<CommandResult> HandleAsync(DeleteInvestmentAccount command, CancellationToken ct)
    {
        var account = await accounts.LoadAsync(command.AccountId, ct);
        var portfolio = await portfolios.LoadAsync(InvestmentPortfolio.IdFor(command.UserId), ct);

        try
        {
            var at = clock.UtcNow;
            var accountEvents = InvestmentAccount.Decide(account, command, at);
            if (accountEvents.Count > 0)
            {
                account!.ApplyAll(accountEvents);
                work.Stage(account, accountEvents);
            }

            var portfolioEvents = InvestmentPortfolio.Decide(portfolio, command, at);
            if (portfolioEvents.Count > 0)
            {
                portfolio!.ApplyAll(portfolioEvents);
                work.Stage(portfolio, portfolioEvents);
            }

            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection);
        }
    }
}
