using PSPad.Abstractions;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Lists;

public sealed class DeleteTaskListHandler(
    IDocumentStore<TaskList> store,
    IDocumentStore<TodoTask> tasks,
    IDocumentStore<ReferenceItem> items,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<DeleteTaskList>
{
    public async Task<CommandResult> HandleAsync(DeleteTaskList command, CancellationToken ct)
    {
        var list = await store.LoadAsync(command.ListId, ct);

        try
        {
            TaskList.Require(list, command.UserId);
            var userTasks = await tasks.LoadAllAsync(command.UserId, ct);
            var userItems = await items.LoadAllAsync(command.UserId, ct);

            foreach (var (aggregate, events) in TaskListCascade.Delete(list, command, userTasks, userItems, clock.UtcNow))
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
