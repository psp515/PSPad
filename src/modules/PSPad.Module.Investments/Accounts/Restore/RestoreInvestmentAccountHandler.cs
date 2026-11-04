using PSPad.Abstractions;

namespace PSPad.Module.Investments.Accounts;

public sealed class RestoreInvestmentAccountHandler(IDocumentStore<InvestmentAccount> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<RestoreInvestmentAccount>
{
    public async Task<CommandResult> HandleAsync(RestoreInvestmentAccount command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(command.AccountId, ct);

        try
        {
            var events = InvestmentAccount.Decide(existing, command, clock.UtcNow);
            if (events.Count > 0)
            {
                var changed = existing ?? new InvestmentAccount();
                changed.ApplyAll(events);
                work.Stage(changed, events);
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
