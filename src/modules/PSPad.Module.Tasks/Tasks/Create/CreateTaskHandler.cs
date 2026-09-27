using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;

namespace PSPad.Module.Tasks.Tasks;

public sealed class CreateTaskHandler(
    IDocumentStore<TodoTask> store,
    IDocumentStore<TaskList> lists,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<CreateTask>
{
    public async Task<CommandResult> HandleAsync(CreateTask command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(command.TaskId, ct);

        try
        {
            var events = TodoTask.Decide(existing, command, clock.UtcNow);
            TaskList.Require(await lists.LoadAsync(command.ListId, ct), command.UserId);
            var task = existing ?? new TodoTask();
            task.ApplyAll(events);
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
