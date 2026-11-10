using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;

namespace PSPad.Module.Money.Entries;

public sealed class RecordReturnHandler(
    IDocumentStore<Budget> budgets, IDocumentStore<MoneyEntry> entries, IUnitOfWork work, IClock clock)
    : ICommandHandler<RecordReturn>
{
    public async Task<CommandResult> HandleAsync(RecordReturn command, CancellationToken ct)
    {
        var purchase = await entries.LoadAsync(command.PurchaseId, ct);
        var budget = purchase is null ? null : await budgets.LoadAsync(purchase.BudgetId, ct);
        var entry = await entries.LoadAsync(command.EntryId, ct);
        IReadOnlyList<MoneyEntry> owned = budget is null ? [] : await entries.LoadAllAsync(budget.UserId, ct);

        try
        {
            var events = MoneyEntry.DecideReturn(entry, purchase, budget, owned, command, clock.UtcNow);
            var written = new MoneyEntry();
            written.ApplyAll(events);
            work.Stage(written, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection);
        }
    }
}
