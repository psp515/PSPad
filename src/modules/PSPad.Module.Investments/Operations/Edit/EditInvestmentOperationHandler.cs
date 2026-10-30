using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;

namespace PSPad.Module.Investments.Operations;

public sealed class EditInvestmentOperationHandler(
    IDocumentStore<InvestmentOperation> operations, IDocumentStore<InvestmentAccount> accounts, IUnitOfWork work,
    IClock clock)
    : ICommandHandler<EditInvestmentOperation>
{
    public async Task<CommandResult> HandleAsync(EditInvestmentOperation command, CancellationToken ct)
    {
        var operation = await operations.LoadAsync(command.OperationId, ct);
        var account = operation is null ? null : await accounts.LoadAsync(operation.AccountId, ct);

        try
        {
            var events = InvestmentOperation.Decide(operation, account, command, clock.UtcNow);
            if (events.Count > 0)
            {
                operation!.ApplyAll(events);
                work.Stage(operation, events);
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
