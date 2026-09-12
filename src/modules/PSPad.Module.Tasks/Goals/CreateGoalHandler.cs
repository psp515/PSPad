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
