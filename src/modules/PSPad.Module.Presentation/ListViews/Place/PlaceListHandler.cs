using PSPad.Abstractions;

namespace PSPad.Module.Presentation.ListViews;

public sealed class PlaceListHandler(IDocumentStore<ListView> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<PlaceList>
{
    public async Task<CommandResult> HandleAsync(PlaceList command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(ListView.IdFor(command.UserId, command.ListId), ct);

        try
        {
            var events = ListView.Decide(existing, command, clock.UtcNow);
            var view = existing ?? new ListView();
            view.ApplyAll(events);
            work.Stage(view, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
