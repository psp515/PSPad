using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;

namespace PSPad.Module.Investments.Operations;

public sealed class RecordInvestmentOperationHandler(
    IDocumentStore<InvestmentOperation> operations, IDocumentStore<InvestmentAccount> accounts, IUnitOfWork work,
    IClock clock)
    : ICommandHandler<RecordInvestmentOperation>
{
    public async Task<CommandResult> HandleAsync(RecordInvestmentOperation command, CancellationToken ct)
    {
        var operation = await operations.LoadAsync(command.OperationId, ct);
        var account = await accounts.LoadAsync(command.AccountId, ct);

        try
        {
            var at = clock.UtcNow;
            var operationEvents = InvestmentOperation.Decide(operation, account, command, at);
            var accountEvents = InvestmentAccount.Count(account, operation, command, at);

            if (operationEvents.Count > 0)
            {
                var recorded = new InvestmentOperation();
                recorded.ApplyAll(operationEvents);
                work.Stage(recorded, operationEvents);
            }

            if (accountEvents.Count > 0)
            {
                account!.ApplyAll(accountEvents);
                work.Stage(account, accountEvents);
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
