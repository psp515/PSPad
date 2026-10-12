using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed class AddCategoryHandler(IDocumentStore<Budget> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<AddCategory>
{
    public async Task<CommandResult> HandleAsync(AddCategory command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(command.BudgetId, ct);

        try
        {
            var events = Budget.Decide(existing, command, clock.UtcNow);
            var budget = existing ?? new Budget();
            budget.ApplyAll(events);
            work.Stage(budget, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
