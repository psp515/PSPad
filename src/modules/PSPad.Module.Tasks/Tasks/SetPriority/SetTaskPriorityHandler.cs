using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed class SetTaskPriorityHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<SetTaskPriority>
{
    public async Task<CommandResult> HandleAsync(SetTaskPriority command, CancellationToken ct)
    {
        var task = await store.LoadAsync(command.TaskId, ct);

        try
        {
            var events = TodoTask.Decide(task, command, clock.UtcNow);
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
