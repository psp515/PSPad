using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;

namespace PSPad.Module.Money.Balance;

public sealed class DeleteBalanceMonthHandler(
    IDocumentStore<Budget> budgets, IDocumentStore<BalanceSnapshot> snapshots, IUnitOfWork work, IClock clock)
    : ICommandHandler<DeleteBalanceMonth>
{
    public async Task<CommandResult> HandleAsync(DeleteBalanceMonth command, CancellationToken ct)
    {
        var budget = await budgets.LoadAsync(command.BudgetId, ct);
        var snapshot = budget is null
            ? null
            : await snapshots.LoadAsync(BalanceSnapshot.IdFor(budget.Id, command.Month), ct);

        try
        {
            var events = BalanceSnapshot.Decide(snapshot, budget, command, clock.UtcNow);
            snapshot!.ApplyAll(events);
            work.Stage(snapshot, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
