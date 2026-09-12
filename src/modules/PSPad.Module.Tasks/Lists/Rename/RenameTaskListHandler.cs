using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Lists;

public sealed class RenameTaskListHandler(IDocumentStore<TaskList> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<RenameTaskList>
{
    public async Task<CommandResult> HandleAsync(RenameTaskList command, CancellationToken ct)
    {
        var list = await store.LoadAsync(command.ListId, ct);

        try
        {
            var events = TaskList.Decide(list, command, clock.UtcNow);
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
