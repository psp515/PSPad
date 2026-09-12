using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tasks;

public sealed class CreateTaskHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<CreateTask>
{
    public async Task<CommandResult> HandleAsync(CreateTask command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(command.TaskId, ct);

        try
        {
            var events = TodoTask.Decide(existing, command, clock.UtcNow);
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

public sealed class RenameTaskHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<RenameTask>
{
    public async Task<CommandResult> HandleAsync(RenameTask command, CancellationToken ct)
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

public sealed class SetTaskDueDateHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<SetTaskDueDate>
{
    public async Task<CommandResult> HandleAsync(SetTaskDueDate command, CancellationToken ct)
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

public sealed class StarTaskHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<StarTask>
{
    public async Task<CommandResult> HandleAsync(StarTask command, CancellationToken ct)
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

public sealed class LinkTaskToGoalHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<LinkTaskToGoal>
{
    public async Task<CommandResult> HandleAsync(LinkTaskToGoal command, CancellationToken ct)
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

public sealed class MoveTaskToListHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<MoveTaskToList>
{
    public async Task<CommandResult> HandleAsync(MoveTaskToList command, CancellationToken ct)
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

public sealed class CompleteTaskHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<CompleteTask>
{
    public async Task<CommandResult> HandleAsync(CompleteTask command, CancellationToken ct)
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

public sealed class ReopenTaskHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<ReopenTask>
{
    public async Task<CommandResult> HandleAsync(ReopenTask command, CancellationToken ct)
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

public sealed class DeleteTaskHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<DeleteTask>
{
    public async Task<CommandResult> HandleAsync(DeleteTask command, CancellationToken ct)
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

public sealed class AddStepHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<AddStep>
{
    public async Task<CommandResult> HandleAsync(AddStep command, CancellationToken ct)
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

public sealed class RenameStepHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<RenameStep>
{
    public async Task<CommandResult> HandleAsync(RenameStep command, CancellationToken ct)
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

public sealed class SetStepDueDateHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<SetStepDueDate>
{
    public async Task<CommandResult> HandleAsync(SetStepDueDate command, CancellationToken ct)
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

public sealed class CheckStepHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<CheckStep>
{
    public async Task<CommandResult> HandleAsync(CheckStep command, CancellationToken ct)
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

public sealed class MoveStepHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<MoveStep>
{
    public async Task<CommandResult> HandleAsync(MoveStep command, CancellationToken ct)
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

public sealed class RemoveStepHandler(IDocumentStore<TodoTask> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<RemoveStep>
{
    public async Task<CommandResult> HandleAsync(RemoveStep command, CancellationToken ct)
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
