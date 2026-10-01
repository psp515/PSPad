using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;

namespace PSPad.Module.Tasks.Tasks;

public sealed class RenameTaskHandler(
    IDocumentStore<TodoTask> store,
    IDocumentStore<TaskList> lists,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<RenameTask>
{
    public async Task<CommandResult> HandleAsync(RenameTask command, CancellationToken ct)
    {
        var task = await store.LoadAsync(command.TaskId, ct);

        try
        {
            var access = await lists.AccessAsync(task?.ListId, command.UserId, ct);
            var events = TodoTask.Decide(task, command, clock.UtcNow, access);
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
