using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;

namespace PSPad.Module.Money.Entries;

public sealed class EditEntryHandler(
    IDocumentStore<Budget> budgets, IDocumentStore<MoneyEntry> entries, IUnitOfWork work, IClock clock)
    : ICommandHandler<EditEntry>
{
    public async Task<CommandResult> HandleAsync(EditEntry command, CancellationToken ct)
    {
        var entry = await entries.LoadAsync(command.EntryId, ct);
        var budget = entry is null ? null : await budgets.LoadAsync(entry.BudgetId, ct);

        try
        {
            foreach (var (aggregate, events) in MoneyEntryCascade.Write(entry, budget, command, clock.UtcNow))
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
