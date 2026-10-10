using PSPad.Abstractions;
using PSPad.Module.Money.Entries;

namespace PSPad.Module.Money.Budgets;

public sealed class RemoveCategoryHandler(
    IDocumentStore<Budget> budgets, IDocumentStore<MoneyEntry> entries, IUnitOfWork work, IClock clock)
    : ICommandHandler<RemoveCategory>
{
    public async Task<CommandResult> HandleAsync(RemoveCategory command, CancellationToken ct)
    {
        var budget = await budgets.LoadAsync(command.BudgetId, ct);
        IReadOnlyList<MoneyEntry> owned = budget is null ? [] : await entries.LoadAllAsync(budget.UserId, ct);

        try
        {
            foreach (var (aggregate, events) in CategoryCascade.Remove(budget, command, owned, clock.UtcNow))
            {
                work.Stage(aggregate, events);
            }

            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
