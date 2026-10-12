using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Goals;

public sealed class AddTaskToGoalHandler(
    IDocumentStore<Goal> goals,
    IDocumentStore<TodoTask> tasks,
    IDocumentStore<TaskList> lists,
    IUnitOfWork work,
    IClock clock) : ICommandHandler<AddTaskToGoal>
{
    public async Task<CommandResult> HandleAsync(AddTaskToGoal command, CancellationToken ct)
    {
        var goal = await goals.LoadAsync(command.GoalId, ct);
        var task = await tasks.LoadAsync(command.TaskId, ct);

        try
        {
            var access = await lists.AccessAsync(task?.ListId, command.UserId, ct);
            var visible = task is { Deleted: false } && task.UserId == access.OwnerId ? task : null;
            var events = Goal.Decide(goal, command, clock.UtcNow, visible);
            goal!.ApplyAll(events);
            work.Stage(goal, events);

            if (events.Count > 0)
            {
                foreach (var other in await goals.LoadAllAsync(command.UserId, ct))
                {
                    var moved = Goal.MoveOut(other, goal, visible!, clock.UtcNow);
                    if (moved.Count > 0)
                    {
                        other.ApplyAll(moved);
                        work.Stage(other, moved);
                    }
                }
            }

            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection.Message);
        }
    }
}
