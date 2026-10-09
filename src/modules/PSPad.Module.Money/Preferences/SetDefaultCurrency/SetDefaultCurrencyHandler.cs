using PSPad.Abstractions;

namespace PSPad.Module.Money.Preferences;

public sealed class SetDefaultCurrencyHandler(IDocumentStore<MoneyPreferences> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<SetDefaultCurrency>
{
    public async Task<CommandResult> HandleAsync(SetDefaultCurrency command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(MoneyPreferences.IdFor(command.UserId), ct);

        try
        {
            var events = MoneyPreferences.Decide(existing, command, clock.UtcNow);
            var preferences = existing ?? new MoneyPreferences();
            preferences.ApplyAll(events);
            work.Stage(preferences, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
