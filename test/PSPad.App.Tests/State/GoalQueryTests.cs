using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class GoalQueryTests
{
    [Fact]
    public void AUrlWithNoQueryOpensNoGoal()
    {
        Assert.Null(GoalQuery.From("https://pspad.local/goals"));
        Assert.False(GoalQuery.IsNew("https://pspad.local/goals"));
    }

    [Fact]
    public void AUrlWithAGoalOpensIt()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, GoalQuery.From($"https://pspad.local/goals?goal={id}"));
    }

    [Fact]
    public void ANewGoalUrlIsNewAndOpensNoExistingGoal()
    {
        var uri = GoalQuery.ForNew("https://pspad.local/goals?task=" + Guid.NewGuid());

        Assert.Equal("https://pspad.local/goals?goal=new", uri);
        Assert.True(GoalQuery.IsNew(uri));
        Assert.Null(GoalQuery.From(uri));
    }

    [Fact]
    public void EditingAGoalKeepsTheScreen()
    {
        var id = Guid.NewGuid();

        Assert.Equal($"https://pspad.local/goals?goal={id}", GoalQuery.For("https://pspad.local/goals", id));
    }
}
