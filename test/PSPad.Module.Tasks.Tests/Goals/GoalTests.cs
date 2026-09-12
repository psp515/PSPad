using PSPad.Abstractions;
using PSPad.Module.Tasks.Goals;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Goals;

[UnitTest]
public class GoalTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AGoalIsCreatedOpen()
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), "Eat better"), Now));

        Assert.Equal("Eat better", goal.Name);
        Assert.False(goal.Achieved);
    }

    [Fact]
    public void AchievingAnAchievedGoalProducesNoEvent()
    {
        var goal = Existing();
        goal.ApplyAll(Goal.Decide(goal, new AchieveGoal(Guid.NewGuid(), User, goal.Id), Now));

        Assert.Empty(Goal.Decide(goal, new AchieveGoal(Guid.NewGuid(), User, goal.Id), Now));
    }

    [Fact]
    public void ReopeningAnAchievedGoalClearsIt()
    {
        var goal = Existing();
        goal.ApplyAll(Goal.Decide(goal, new AchieveGoal(Guid.NewGuid(), User, goal.Id), Now));
        goal.ApplyAll(Goal.Decide(goal, new ReopenGoal(Guid.NewGuid(), User, goal.Id), Now));

        Assert.False(goal.Achieved);
    }

    static Goal Existing()
    {
        var goal = new Goal();
        goal.Apply(new GoalCreated(Guid.NewGuid(), User, Now, "Eat better"));
        return goal;
    }
}
