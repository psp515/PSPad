using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;

namespace PSPad.Module.Tasks.Lists;

public sealed class MoveTaskListToAreaHandler(
    IDocumentStore<TaskList> store,
    IDocumentStore<Area> areas,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<MoveTaskListToArea>
{
    public async Task<CommandResult> HandleAsync(MoveTaskListToArea command, CancellationToken ct)
    {
        var list = await store.LoadAsync(command.ListId, ct);

        try
        {
            var events = TaskList.Decide(list, command, clock.UtcNow);
            if (events.Count > 0)
            {
                Area.Require(await areas.LoadAsync(command.AreaId, ct), command.UserId);
            }

            list!.ApplyAll(events);
            work.Stage(list, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
