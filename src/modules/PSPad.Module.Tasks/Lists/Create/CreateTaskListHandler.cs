using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;

namespace PSPad.Module.Tasks.Lists;

public sealed class CreateTaskListHandler(
    IDocumentStore<TaskList> store,
    IDocumentStore<Area> areas,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<CreateTaskList>
{
    public async Task<CommandResult> HandleAsync(CreateTaskList command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(command.ListId, ct);

        try
        {
            var events = TaskList.Decide(existing, command, clock.UtcNow);
            Area.Require(await areas.LoadAsync(command.AreaId, ct), command.UserId);
            var list = existing ?? new TaskList();
            list.ApplyAll(events);
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
