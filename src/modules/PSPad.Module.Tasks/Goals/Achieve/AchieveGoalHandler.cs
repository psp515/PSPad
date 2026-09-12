using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

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
