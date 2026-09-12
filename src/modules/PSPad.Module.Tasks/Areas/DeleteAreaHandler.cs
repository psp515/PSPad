using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Areas;

public sealed class DeleteAreaHandler(IDocumentStore<Area> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<DeleteArea>
{
    public async Task<CommandResult> HandleAsync(DeleteArea command, CancellationToken ct)
    {
        var area = await store.LoadAsync(command.AreaId, ct);

        try
        {
            var events = Area.Decide(area, command, clock.UtcNow);
            area!.ApplyAll(events);
            work.Stage(area, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
