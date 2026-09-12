using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Areas;

public sealed class CreateAreaHandler(IDocumentStore<Area> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<CreateArea>
{
    public async Task<CommandResult> HandleAsync(CreateArea command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(command.AreaId, ct);

        try
        {
            var events = Area.Decide(existing, command, clock.UtcNow);
            var area = existing ?? new Area();
            area.ApplyAll(events);
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
