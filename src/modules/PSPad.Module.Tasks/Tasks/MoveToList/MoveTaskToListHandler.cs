using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;

namespace PSPad.Module.Tasks.Tasks;

public sealed class MoveTaskToListHandler(
    IDocumentStore<TodoTask> store,
    IDocumentStore<TaskList> lists,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<MoveTaskToList>
{
    public async Task<CommandResult> HandleAsync(MoveTaskToList command, CancellationToken ct)
    {
        var task = await store.LoadAsync(command.TaskId, ct);

        try
        {
            var access = await lists.AccessAsync(task?.ListId, command.UserId, ct);
            var events = TodoTask.Decide(task, command, clock.UtcNow, access);
            if (events.Count > 0)
            {
                var target = TaskList.RequireAcceptsTasks(await lists.LoadAsync(command.ListId, ct), command.UserId);
                if (target.OwnerId != access.OwnerId)
                {
                    throw new DomainRejectedException("A task can only move between lists of the same owner.");
                }
            }

            task!.ApplyAll(events);
            work.Stage(task, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
