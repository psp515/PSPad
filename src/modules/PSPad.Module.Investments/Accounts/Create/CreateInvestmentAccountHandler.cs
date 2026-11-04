using PSPad.Abstractions;
using PSPad.Module.Investments.Portfolio;

namespace PSPad.Module.Investments.Accounts;

public sealed class CreateInvestmentAccountHandler(
    IDocumentStore<InvestmentAccount> accounts, IDocumentStore<InvestmentPortfolio> portfolios, IUnitOfWork work,
    IClock clock)
    : ICommandHandler<CreateInvestmentAccount>
{
    public async Task<CommandResult> HandleAsync(CreateInvestmentAccount command, CancellationToken ct)
    {
        var existing = await accounts.LoadAsync(command.AccountId, ct);
        var portfolio = await portfolios.LoadAsync(InvestmentPortfolio.IdFor(command.UserId), ct);

        try
        {
            var at = clock.UtcNow;
            var accountEvents = InvestmentAccount.Decide(existing, command, at);
            var portfolioEvents = InvestmentPortfolio.Decide(portfolio, command, at);

            var account = new InvestmentAccount();
            account.ApplyAll(accountEvents);
            work.Stage(account, accountEvents);

            if (portfolioEvents.Count > 0)
            {
                var counted = portfolio ?? new InvestmentPortfolio();
                counted.ApplyAll(portfolioEvents);
                work.Stage(counted, portfolioEvents);
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
