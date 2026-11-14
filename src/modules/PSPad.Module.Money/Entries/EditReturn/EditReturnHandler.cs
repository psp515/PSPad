using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;

namespace PSPad.Module.Money.Entries;

public sealed class EditReturnHandler(
    IDocumentStore<Budget> budgets, IDocumentStore<MoneyEntry> entries, IUnitOfWork work, IClock clock)
    : ICommandHandler<EditReturn>
{
    public async Task<CommandResult> HandleAsync(EditReturn command, CancellationToken ct)
    {
        var entry = await entries.LoadAsync(command.EntryId, ct);
        var purchase = entry?.ReturnOf is { } id ? await entries.LoadAsync(id, ct) : null;
        var budget = entry is null ? null : await budgets.LoadAsync(entry.BudgetId, ct);
        IReadOnlyList<MoneyEntry> owned = budget is null ? [] : await entries.LoadAllAsync(budget.UserId, ct);

        try
        {
            var events = MoneyEntry.DecideReturn(entry, purchase, budget, owned, command, clock.UtcNow);
            entry!.ApplyAll(events);
            work.Stage(entry, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection);
        }
    }
}
