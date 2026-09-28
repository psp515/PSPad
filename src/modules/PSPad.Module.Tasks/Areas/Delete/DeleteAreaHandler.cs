using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Areas;

public sealed class DeleteAreaHandler(
    IDocumentStore<Area> store,
    IDocumentStore<TaskList> lists,
    IDocumentStore<TodoTask> tasks,
    IDocumentStore<ReferenceItem> items,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<DeleteArea>
{
    public async Task<CommandResult> HandleAsync(DeleteArea command, CancellationToken ct)
    {
        var area = await store.LoadAsync(command.AreaId, ct);

        try
        {
            var at = clock.UtcNow;
            var events = Area.Decide(area, command, at);
            area!.ApplyAll(events);
            work.Stage(area, events);

            var doomedLists = (await lists.LoadAllAsync(command.UserId, ct))
                .Where(list => list.AreaId == area.Id && !list.Deleted)
                .ToArray();

            if (doomedLists.Length > 0)
            {
                var userTasks = await tasks.LoadAllAsync(command.UserId, ct);
                var userItems = await items.LoadAllAsync(command.UserId, ct);

                foreach (var list in doomedLists)
                {
                    var deleteList = new DeleteTaskList(command.CommandId, command.UserId, list.Id);

                    foreach (var (aggregate, listEvents) in TaskListCascade.Delete(list, deleteList, userTasks, userItems, at))
                    {
                        work.Stage(aggregate, listEvents);
                    }
                }
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
