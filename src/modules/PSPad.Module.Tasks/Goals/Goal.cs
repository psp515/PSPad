using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Goals;

public sealed class Goal : Aggregate
{
    public string Name { get; private set; } = "";
    public bool Achieved { get; private set; }

    public static IReadOnlyList<DomainEvent> Decide(Goal? goal, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case CreateGoal create:
                if (goal is not null)
                {
                    throw new DomainRejectedException("That goal already exists.");
                }

                return [new GoalCreated(create.GoalId, create.UserId, at, RequireName(create.Name))];

            case RenameGoal rename:
                var renaming = Require(goal, rename.UserId);
                var name = RequireName(rename.Name);
                return renaming.Name == name
                    ? []
                    : [new GoalRenamed(renaming.Id, rename.UserId, at, name)];

            case AchieveGoal achieve:
                var achieving = Require(goal, achieve.UserId);
                return achieving.Achieved ? [] : [new GoalAchieved(achieving.Id, achieve.UserId, at)];

            case ReopenGoal reopen:
                var reopening = Require(goal, reopen.UserId);
                return reopening.Achieved ? [new GoalReopened(reopening.Id, reopen.UserId, at)] : [];

            case DeleteGoal delete:
                var deleting = Require(goal, delete.UserId);
                return deleting.Deleted ? [] : [new GoalDeleted(deleting.Id, delete.UserId, at)];

            default:
                throw new DomainRejectedException($"A goal cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case GoalCreated created:
                Id = created.AggregateId;
                UserId = created.UserId;
                Name = created.Name;
                break;
            case GoalRenamed renamed:
                Name = renamed.Name;
                break;
            case GoalAchieved:
                Achieved = true;
                break;
            case GoalReopened:
                Achieved = false;
                break;
            case GoalDeleted:
                Deleted = true;
                break;
        }
    }

    static Goal Require(Goal? goal, Guid userId)
    {
        if (goal is null || goal.Deleted)
        {
            throw new DomainRejectedException("That goal no longer exists.");
        }

        if (goal.UserId != userId)
        {
            throw new DomainRejectedException("That goal belongs to somebody else.");
        }

        return goal;
    }

    static string RequireName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new DomainRejectedException("A goal needs a name.")
            : name.Trim();
}
