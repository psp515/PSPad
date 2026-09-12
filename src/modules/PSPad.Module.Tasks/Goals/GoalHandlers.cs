using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed class CreateGoalHandler(IDocumentStore<Goal> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<CreateGoal>
{
    public async Task<CommandResult> HandleAsync(CreateGoal command, CancellationToken ct)
    {
        var existing = await store.LoadAsync(command.GoalId, ct);

        try
        {
            var events = Goal.Decide(existing, command, clock.UtcNow);
            var goal = existing ?? new Goal();
            goal.ApplyAll(events);
            work.Stage(goal, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}

public sealed class RenameGoalHandler(IDocumentStore<Goal> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<RenameGoal>
{
    public async Task<CommandResult> HandleAsync(RenameGoal command, CancellationToken ct)
    {
        var goal = await store.LoadAsync(command.GoalId, ct);

        try
        {
            var events = Goal.Decide(goal, command, clock.UtcNow);
            goal!.ApplyAll(events);
            work.Stage(goal, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}

public sealed class AchieveGoalHandler(IDocumentStore<Goal> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<AchieveGoal>
{
    public async Task<CommandResult> HandleAsync(AchieveGoal command, CancellationToken ct)
    {
        var goal = await store.LoadAsync(command.GoalId, ct);

        try
        {
            var events = Goal.Decide(goal, command, clock.UtcNow);
            goal!.ApplyAll(events);
            work.Stage(goal, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}

public sealed class ReopenGoalHandler(IDocumentStore<Goal> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<ReopenGoal>
{
    public async Task<CommandResult> HandleAsync(ReopenGoal command, CancellationToken ct)
    {
        var goal = await store.LoadAsync(command.GoalId, ct);

        try
        {
            var events = Goal.Decide(goal, command, clock.UtcNow);
            goal!.ApplyAll(events);
            work.Stage(goal, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}

public sealed class DeleteGoalHandler(IDocumentStore<Goal> store, IUnitOfWork work, IClock clock)
    : ICommandHandler<DeleteGoal>
{
    public async Task<CommandResult> HandleAsync(DeleteGoal command, CancellationToken ct)
    {
        var goal = await store.LoadAsync(command.GoalId, ct);

        try
        {
            var events = Goal.Decide(goal, command, clock.UtcNow);
            goal!.ApplyAll(events);
            work.Stage(goal, events);
            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
