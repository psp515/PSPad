using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class StatusBeltsTests
{
    [Fact]
    public void ABeltShowsWithItsTextAndAction()
    {
        var belts = new StatusBelts();
        Func<Task> reload = () => Task.CompletedTask;

        belts.Show(BeltKind.Update, "A new version of PSPad is ready.", "Reload", reload);

        var belt = Assert.Single(belts.Visible);
        Assert.Equal(BeltKind.Update, belt.Kind);
        Assert.Equal("A new version of PSPad is ready.", belt.Text);
        Assert.Equal("Reload", belt.ActionText);
        Assert.Same(reload, belt.Action);
    }

    [Fact]
    public void ABeltNeedsNoAction()
    {
        var belts = new StatusBelts();

        belts.Show(BeltKind.Offline, "Offline.");

        var belt = Assert.Single(belts.Visible);
        Assert.Null(belt.ActionText);
        Assert.Null(belt.Action);
    }

    [Fact]
    public void EachKindHoldsOneBeltAndShowingAgainReplacesIt()
    {
        var belts = new StatusBelts();

        belts.Show(BeltKind.Rejected, "1 change couldn’t be saved.");
        belts.Show(BeltKind.Rejected, "2 changes couldn’t be saved.");

        Assert.Equal("2 changes couldn’t be saved.", Assert.Single(belts.Visible).Text);
    }

    [Fact]
    public void TheNewestBeltComesFirst()
    {
        var belts = new StatusBelts();

        belts.Show(BeltKind.Update, "update");
        belts.Show(BeltKind.Offline, "offline");
        belts.Show(BeltKind.Rejected, "rejected");

        Assert.Equal([BeltKind.Rejected, BeltKind.Offline, BeltKind.Update], belts.Visible.Select(belt => belt.Kind));
    }

    [Fact]
    public void ShowingAKindAgainMovesItToTheTop()
    {
        var belts = new StatusBelts();

        belts.Show(BeltKind.Update, "update");
        belts.Show(BeltKind.Offline, "offline");
        belts.Show(BeltKind.Update, "update again");

        Assert.Equal([BeltKind.Update, BeltKind.Offline], belts.Visible.Select(belt => belt.Kind));
    }

    [Fact]
    public void ADismissedBeltStaysHiddenWhileItsStateLasts()
    {
        var belts = new StatusBelts();
        belts.Show(BeltKind.Offline, "offline");

        belts.Dismiss(BeltKind.Offline);
        belts.Show(BeltKind.Offline, "offline");

        Assert.Empty(belts.Visible);
    }

    [Fact]
    public void DismissingOneKindLeavesTheOthers()
    {
        var belts = new StatusBelts();
        belts.Show(BeltKind.Update, "update");
        belts.Show(BeltKind.Offline, "offline");

        belts.Dismiss(BeltKind.Offline);

        Assert.Equal(BeltKind.Update, Assert.Single(belts.Visible).Kind);
    }

    [Fact]
    public void ClearingHidesTheBelt()
    {
        var belts = new StatusBelts();
        belts.Show(BeltKind.Offline, "offline");

        belts.Clear(BeltKind.Offline);

        Assert.Empty(belts.Visible);
    }

    [Fact]
    public void OnceTheStateEndsTheNextShowAppearsAgain()
    {
        var belts = new StatusBelts();
        belts.Show(BeltKind.Offline, "offline");
        belts.Dismiss(BeltKind.Offline);

        belts.Clear(BeltKind.Offline);
        belts.Show(BeltKind.Offline, "offline again");

        Assert.Equal("offline again", Assert.Single(belts.Visible).Text);
    }

    [Fact]
    public void EveryChangeIsAnnounced()
    {
        var belts = new StatusBelts();
        var changes = 0;
        belts.Changed += () => changes++;

        belts.Show(BeltKind.Update, "update");
        belts.Dismiss(BeltKind.Update);
        belts.Clear(BeltKind.Update);

        Assert.Equal(3, changes);
    }

    [Fact]
    public void NothingThatChangesNothingIsAnnounced()
    {
        var belts = new StatusBelts();
        belts.Show(BeltKind.Offline, "offline");
        belts.Dismiss(BeltKind.Offline);
        var changes = 0;
        belts.Changed += () => changes++;

        belts.Show(BeltKind.Offline, "offline");
        belts.Dismiss(BeltKind.Offline);
        belts.Clear(BeltKind.Update);

        Assert.Equal(0, changes);
    }
}
