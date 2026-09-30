using PSPad.Abstractions;

namespace PSPad.Module.Presentation.AreaViews;

public sealed class ReorderListsHandler(IDocumentStore<AreaView> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<ReorderLists>
{
    public async Task<CommandResult> HandleAsync(ReorderLists command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(AreaView.IdFor(command.UserId, command.AreaId), ct);

        try
        {
            var events = AreaView.Decide(existing, command, clock.UtcNow);
            var view = existing ?? new AreaView();
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
