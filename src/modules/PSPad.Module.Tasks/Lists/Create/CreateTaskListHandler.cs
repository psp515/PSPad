using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed class CreateTaskListHandler(IDocumentStore<TaskList> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<CreateTaskList>
{
    public async Task<CommandResult> HandleAsync(CreateTaskList command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(command.ListId, ct);

        try
        {
            var events = TaskList.Decide(existing, command, clock.UtcNow);
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
