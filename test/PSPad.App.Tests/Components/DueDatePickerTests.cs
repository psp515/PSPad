using Bunit;
using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class DueDatePickerTests : Bunit.TestContext
{
    static readonly DateOnly Thursday = new(2026, 9, 24);

    [Fact]
    public void QuickPicksOfferTodayTomorrowInTwoDaysAndNextMonday()
    {
        var picks = DueDatePicker.QuickPicks(Thursday);

        Assert.Equal(["Today", "Tomorrow", "In 2 days", "Next week"], picks.Select(pick => pick.Label));
        Assert.Equal(
            [Thursday, Thursday.AddDays(1), Thursday.AddDays(2), new DateOnly(2026, 9, 28)],
            picks.Select(pick => pick.Day));
    }

    [Fact]
    public void OnAMondayNextWeekIsTheFollowingMonday()
    {
        var monday = new DateOnly(2026, 9, 28);

        Assert.Equal(monday.AddDays(7), DueDatePicker.QuickPicks(monday)[3].Day);
    }

    [Fact]
    public void ClickingAQuickPickSetsThatDay()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), Thursday);
        DateOnly? picked = null;

        var picker = Render<DueDatePicker>(parameters => parameters
            .Add(p => p.Today, Thursday)
            .Add(p => p.ValueChanged, (DateOnly? day) => picked = day));
        picker.FindAll(".pspad-due-quick")[1].Click();

        Assert.Equal(Thursday.AddDays(1), picked);
    }

    [Fact]
    public void TheQuickPickMatchingTheValueIsFilled()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), Thursday);

        var picker = Render<DueDatePicker>(parameters => parameters
            .Add(p => p.Today, Thursday)
            .Add(p => p.Value, (DateOnly?)Thursday.AddDays(2)));

        var chips = picker.FindAll(".pspad-due-quick");
        Assert.Contains("mud-chip-filled", chips[2].ClassName);
        Assert.DoesNotContain("mud-chip-filled", chips[0].ClassName);
    }

    [Fact]
    public void WhileDisabledTheQuickPicksCannotBeClicked()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), Thursday);

        var picker = Render<DueDatePicker>(parameters => parameters
            .Add(p => p.Today, Thursday)
            .Add(p => p.Disabled, true)
            .Add(p => p.ValueChanged, (DateOnly? _) => { }));

        Assert.All(picker.FindAll(".pspad-due-quick"), chip => Assert.Contains("mud-disabled", chip.ClassName));
    }
}
