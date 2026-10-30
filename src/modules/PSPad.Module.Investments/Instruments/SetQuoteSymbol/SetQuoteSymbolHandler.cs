using PSPad.Abstractions;

namespace PSPad.Module.Investments.Instruments;

public sealed class SetQuoteSymbolHandler(IDocumentStore<Instrument> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<SetQuoteSymbol>
{
    public async Task<CommandResult> HandleAsync(SetQuoteSymbol command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(Instrument.IdFor(command.UserId, command.Ticker), ct);

        try
        {
            var events = Instrument.Decide(existing, command, clock.UtcNow);
            if (events.Count > 0)
            {
                var changed = existing ?? new Instrument();
                changed.ApplyAll(events);
                work.Stage(changed, events);
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
