using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class FabContextTests
{
    static FabAction Action(string label) => new(label, "icon", () => Task.CompletedTask);

    [Fact]
    public void ItStartsWithNothingToDo()
    {
        var context = new FabContext();

        Assert.Null(context.Primary);
        Assert.Empty(context.Secondary);
    }

    [Fact]
    public void SettingRecordsThePrimaryAndTheRest()
    {
        var context = new FabContext();

        context.Set(Action("Capture"), Action("New task"), Action("New area"));

        Assert.Equal("Capture", context.Primary!.Label);
        Assert.Equal(["New task", "New area"], context.Secondary.Select(a => a.Label));
    }

    [Fact]
    public void SettingRaisesChanged()
    {
        var context = new FabContext();
        var raised = 0;
        context.Changed += () => raised++;

        context.Set(Action("Capture"));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void ClearingEmptiesItAndRaisesChanged()
    {
        var context = new FabContext();
        context.Set(Action("Capture"), Action("New task"));
        var raised = 0;
        context.Changed += () => raised++;

        context.Clear();

        Assert.Null(context.Primary);
        Assert.Empty(context.Secondary);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ASecondSetReplacesTheFirst()
    {
        var context = new FabContext();
        context.Set(Action("Capture"), Action("New task"));

        context.Set(Action("New goal"));

        Assert.Equal("New goal", context.Primary!.Label);
        Assert.Empty(context.Secondary);
    }
}
