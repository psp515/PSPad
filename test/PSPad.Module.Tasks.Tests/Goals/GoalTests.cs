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

    [Fact]
    public void ANewGoalIsInProgressWithNoDueDate()
    {
        var goal = Existing();

        Assert.Equal(GoalStatus.InProgress, goal.Status);
        Assert.Null(goal.DueOn);
    }

    [Theory]
    [InlineData(GoalStatus.Achieved)]
    [InlineData(GoalStatus.NotAchieved)]
    public void SettingAStatusRecordsIt(GoalStatus status)
    {
        var goal = Existing();
        goal.ApplyAll(Goal.Decide(goal, new SetGoalStatus(Guid.NewGuid(), User, goal.Id, status), Now));

        Assert.Equal(status, goal.Status);
        Assert.Equal(status == GoalStatus.Achieved, goal.Achieved);
    }

    [Fact]
    public void MovingFromNotAchievedToAchievedLeavesOnlyAchieved()
    {
        var goal = Existing();
        goal.ApplyAll(Goal.Decide(goal, new SetGoalStatus(Guid.NewGuid(), User, goal.Id, GoalStatus.NotAchieved), Now));
        goal.ApplyAll(Goal.Decide(goal, new SetGoalStatus(Guid.NewGuid(), User, goal.Id, GoalStatus.Achieved), Now));

        Assert.Equal(GoalStatus.Achieved, goal.Status);
        Assert.False(goal.NotAchieved);
    }

    [Fact]
    public void SettingTheSameStatusProducesNoEvent()
    {
        var goal = Existing();

        Assert.Empty(Goal.Decide(goal, new SetGoalStatus(Guid.NewGuid(), User, goal.Id, GoalStatus.InProgress), Now));
    }

    [Fact]
    public void AnUnknownStatusIsRejected()
    {
        var goal = Existing();

        Assert.Throws<DomainRejectedException>(() =>
            Goal.Decide(goal, new SetGoalStatus(Guid.NewGuid(), User, goal.Id, (GoalStatus)42), Now));
    }

    [Fact]
    public void ReopeningANotAchievedGoalPutsItBackInProgress()
    {
        var goal = Existing();
        goal.ApplyAll(Goal.Decide(goal, new SetGoalStatus(Guid.NewGuid(), User, goal.Id, GoalStatus.NotAchieved), Now));
        goal.ApplyAll(Goal.Decide(goal, new ReopenGoal(Guid.NewGuid(), User, goal.Id), Now));

        Assert.Equal(GoalStatus.InProgress, goal.Status);
    }

    [Fact]
    public void SettingADueDateRecordsItAndClearingItRemovesIt()
    {
        var goal = Existing();
        var due = new DateOnly(2026, 12, 31);

        goal.ApplyAll(Goal.Decide(goal, new SetGoalDueDate(Guid.NewGuid(), User, goal.Id, due), Now));
        Assert.Equal(due, goal.DueOn);

        goal.ApplyAll(Goal.Decide(goal, new SetGoalDueDate(Guid.NewGuid(), User, goal.Id, null), Now));
        Assert.Null(goal.DueOn);
    }

    [Fact]
    public void SettingTheSameDueDateProducesNoEvent()
    {
        var goal = Existing();

        Assert.Empty(Goal.Decide(goal, new SetGoalDueDate(Guid.NewGuid(), User, goal.Id, null), Now));
    }

    [Fact]
    public void AnotherUsersGoalCannotChangeStatusOrDueDate()
    {
        var goal = Existing();
        var stranger = Guid.NewGuid();

        Assert.Throws<DomainRejectedException>(() =>
            Goal.Decide(goal, new SetGoalStatus(Guid.NewGuid(), stranger, goal.Id, GoalStatus.Achieved), Now));
        Assert.Throws<DomainRejectedException>(() =>
            Goal.Decide(goal, new SetGoalDueDate(Guid.NewGuid(), stranger, goal.Id, new DateOnly(2026, 12, 31)), Now));
    }

    static Goal Existing()
    {
        var goal = new Goal();
        goal.Apply(new GoalCreated(Guid.NewGuid(), User, Now, "Eat better"));
        return goal;
    }
}
