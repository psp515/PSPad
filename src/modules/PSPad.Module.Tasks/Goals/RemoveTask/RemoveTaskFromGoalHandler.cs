using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Goals;

public sealed class RemoveTaskFromGoalHandler(
    IDocumentStore<Goal> goals, IDocumentStore<TodoTask> tasks, IUnitOfWork work, IClock clock)
    : ICommandHandler<RemoveTaskFromGoal>
{
    public async Task<CommandResult> HandleAsync(RemoveTaskFromGoal command, CancellationToken ct)
    {
        var goal = await goals.LoadAsync(command.GoalId, ct);
        var task = await tasks.LoadAsync(command.TaskId, ct);

        try
        {
            var events = Goal.Decide(goal, command, clock.UtcNow, task);
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
