using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;

namespace PSPad.Module.Money.Entries;

public sealed class RecordIncomeHandler(
    IDocumentStore<Budget> budgets, IDocumentStore<MoneyEntry> entries, IUnitOfWork work, IClock clock)
    : ICommandHandler<RecordIncome>
{
    public async Task<CommandResult> HandleAsync(RecordIncome command, CancellationToken ct)
    {
        var budget = await budgets.LoadAsync(command.BudgetId, ct);
        var entry = await entries.LoadAsync(command.EntryId, ct);

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
