using PSPad.Abstractions;
using PSPad.Module.Money.Entries;

namespace PSPad.Module.Money.Budgets;

public sealed class MergeCategoryHandler(
    IDocumentStore<Budget> budgets, IDocumentStore<MoneyEntry> entries, IUnitOfWork work, IClock clock)
    : ICommandHandler<MergeCategory>
{
    public async Task<CommandResult> HandleAsync(MergeCategory command, CancellationToken ct)
    {
        var budget = await budgets.LoadAsync(command.BudgetId, ct);
        IReadOnlyList<MoneyEntry> owned = budget is null ? [] : await entries.LoadAllAsync(budget.UserId, ct);

        try
        {
            foreach (var (aggregate, events) in CategoryCascade.Merge(budget, command, owned, clock.UtcNow))
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
