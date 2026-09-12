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

public sealed class MoveTaskListToAreaHandler(IDocumentStore<TaskList> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<MoveTaskListToArea>
{
    public async Task<CommandResult> HandleAsync(MoveTaskListToArea command, CancellationToken ct)
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

public sealed class DeleteTaskListHandler(IDocumentStore<TaskList> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<DeleteTaskList>
{
    public async Task<CommandResult> HandleAsync(DeleteTaskList command, CancellationToken ct)
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
